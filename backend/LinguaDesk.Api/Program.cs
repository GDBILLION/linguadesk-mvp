using LinguaDesk.Api;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddHttpClient<LibreTranslateService>();
builder.Services.AddScoped<ITranslationService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    if (string.Equals(config["Translation:Provider"], "libretranslate", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<LibreTranslateService>();
    return new DemoTranslationService();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
        .AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddSignalR();

var app = builder.Build();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapGet("/", () => Results.Ok(new { product = "LinguaDesk API", status = "running", docs = "/swagger" }));

// Development scaffold only: create schema and seed sample data. Use migrations for production.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    var attempts = 0;
    while (true)
    {
        try
        {
            await db.Database.EnsureCreatedAsync();
            await SeedData.InitializeAsync(db);
            break;
        }
        catch (Exception ex) when (attempts < 10)
        {
            attempts++;
            logger.LogWarning(ex, "Database not ready; retry {Attempt}/10", attempts);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

var api = app.MapGroup("/api/v1");

api.MapGet("/dashboard", async (AppDbContext db) =>
{
    var conversations = await db.Conversations.Where(c => c.WorkspaceId == DemoIds.WorkspaceId).ToListAsync();
    var monthStart = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
    var used = await db.UsageEvents.Where(x => x.WorkspaceId == DemoIds.WorkspaceId && x.OccurredAt >= monthStart).SumAsync(x => (int?)x.Quantity) ?? 0;
    return Results.Ok(new
    {
        workspace = "Northstar Commerce",
        openConversations = conversations.Count(x => x.Status == "open"),
        pendingConversations = conversations.Count(x => x.Status == "pending"),
        resolvedConversations = conversations.Count(x => x.Status == "resolved"),
        monthlyUsage = used,
        monthlyLimit = 1000,
        languages = new[] { "en", "es", "fr", "zh-CN", "de" }
    });
});

api.MapGet("/conversations", async (AppDbContext db, string? status, string? language, string? search) =>
{
    var query = db.Conversations.Include(c => c.Customer).Where(c => c.WorkspaceId == DemoIds.WorkspaceId);
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(c => c.Status == status);
    if (!string.IsNullOrWhiteSpace(language)) query = query.Where(c => c.CustomerLanguage == language);
    if (!string.IsNullOrWhiteSpace(search))
        query = query.Where(c => c.Customer!.DisplayName.Contains(search) || c.Subject.Contains(search));
    var rows = await query.OrderByDescending(c => c.UpdatedAt).Take(100).Select(c => new
    {
        c.Id, c.Subject, c.CustomerLanguage, c.Status, c.AssignedAgent, c.CreatedAt, c.UpdatedAt,
        customer = c.Customer == null ? "Customer" : c.Customer.DisplayName,
        lastMessage = c.Messages.OrderByDescending(m => m.CreatedAt).Select(m => m.TranslatedText ?? m.OriginalText).FirstOrDefault()
    }).ToListAsync();
    return Results.Ok(rows);
});

api.MapGet("/conversations/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var c = await db.Conversations.Include(x => x.Customer)
        .Where(x => x.Id == id && x.WorkspaceId == DemoIds.WorkspaceId)
        .Select(x => new { x.Id, x.Subject, x.CustomerLanguage, x.Status, x.AssignedAgent, x.CreatedAt, x.UpdatedAt,
            customer = x.Customer!.DisplayName, email = x.Customer.Email })
        .FirstOrDefaultAsync();
    return c is null ? Results.NotFound() : Results.Ok(c);
});

api.MapGet("/conversations/{id:guid}/messages", async (Guid id, AppDbContext db) =>
{
    var exists = await db.Conversations.AnyAsync(c => c.Id == id && c.WorkspaceId == DemoIds.WorkspaceId);
    if (!exists) return Results.NotFound();
    var messages = await db.Messages.Where(m => m.ConversationId == id).OrderBy(m => m.CreatedAt)
        .Select(m => new { m.Id, m.SenderType, m.SenderName, m.OriginalText, m.OriginalLanguage,
            m.TranslatedText, m.TargetLanguage, m.TranslationStatus, m.DeliveryStatus, m.CreatedAt })
        .ToListAsync();
    return Results.Ok(messages);
});

api.MapPost("/translations/preview", async (TranslationRequest request, ITranslationService translator, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 10000)
        return Results.BadRequest(new { error = "Text is required and must be 10,000 characters or fewer." });
    if (!LanguageCodes.All.Contains(request.SourceLanguage) || !LanguageCodes.All.Contains(request.TargetLanguage))
        return Results.BadRequest(new { error = "Unsupported language. Use en, es, fr, zh-CN, or de." });
    try
    {
        var result = await translator.TranslateAsync(request, ct);
        return Results.Ok(result);
    }
    catch (Exception)
    {
        return Results.Problem("Translation failed. The original text has not been changed.", statusCode: 502);
    }
});

api.MapPost("/conversations/{id:guid}/messages", async (Guid id, SendMessageRequest request, AppDbContext db, ITranslationService translator, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 10000)
        return Results.BadRequest(new { error = "Reply is required and must be 10,000 characters or fewer." });
    if (string.IsNullOrWhiteSpace(request.ApprovedTranslation) || request.ApprovedTranslation.Length > 12000)
        return Results.BadRequest(new { error = "An approved translation is required and must be 12,000 characters or fewer." });

    var conversation = await db.Conversations.Include(c => c.Customer)
        .FirstOrDefaultAsync(c => c.Id == id && c.WorkspaceId == DemoIds.WorkspaceId, ct);
    if (conversation is null) return Results.NotFound();

    // The frontend sends the exact translation that the agent previewed and approved.
    // In production, bind this to an expiring server-side preview token to prevent tampering.
    var message = new Message
    {
        ConversationId = id,
        SenderType = "agent",
        SenderName = "Alex Morgan",
        OriginalText = request.Text.Trim(),
        OriginalLanguage = "en",
        TranslatedText = request.ApprovedTranslation.Trim(),
        TargetLanguage = conversation.CustomerLanguage,
        TranslationStatus = "completed",
        DeliveryStatus = "sent",
        CreatedAt = DateTimeOffset.UtcNow
    };
    db.Messages.Add(message);
    conversation.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/conversations/{id}/messages", new
    {
        message.Id, message.SenderType, message.SenderName, message.OriginalText, message.OriginalLanguage,
        message.TranslatedText, message.TargetLanguage, message.TranslationStatus, message.DeliveryStatus, message.CreatedAt,
        provider = "approved-preview", isDemoTranslation = false
    });
});

api.MapPatch("/conversations/{id:guid}/status", async (Guid id, UpdateStatusRequest request, AppDbContext db) =>
{
    var allowed = new[] { "open", "pending", "resolved" };
    if (!allowed.Contains(request.Status)) return Results.BadRequest(new { error = "Status must be open, pending, or resolved." });
    var conversation = await db.Conversations.FirstOrDefaultAsync(c => c.Id == id && c.WorkspaceId == DemoIds.WorkspaceId);
    if (conversation is null) return Results.NotFound();
    conversation.Status = request.Status;
    conversation.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync();
    return Results.Ok(new { conversation.Id, conversation.Status, conversation.UpdatedAt });
});

api.MapGet("/billing/usage", async (AppDbContext db) =>
{
    var start = new DateTimeOffset(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
    var used = await db.UsageEvents.Where(x => x.WorkspaceId == DemoIds.WorkspaceId && x.OccurredAt >= start).SumAsync(x => (int?)x.Quantity) ?? 0;
    return Results.Ok(new { plan = "Starter (demo)", used, limit = 1000, periodStart = start, periodEnd = start.AddMonths(1) });
});

// Public website chat scaffold. Production must replace the public workspace ID / session approach with signed customer sessions and anti-abuse protection.
api.MapPost("/public/chat/{workspacePublicId:guid}/conversations", async (Guid workspacePublicId, CreateConversationRequest request, AppDbContext db, ITranslationService translator, CancellationToken ct) =>
{
    if (workspacePublicId != DemoIds.WorkspaceId) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.CustomerName) || string.IsNullOrWhiteSpace(request.Message))
        return Results.BadRequest(new { error = "Customer name and message are required." });
    if (!LanguageCodes.All.Contains(request.Language)) return Results.BadRequest(new { error = "Unsupported language." });

    var customer = new Customer { WorkspaceId = DemoIds.WorkspaceId, DisplayName = request.CustomerName.Trim(), Email = request.Email, Language = request.Language };
    var conversation = new Conversation { WorkspaceId = DemoIds.WorkspaceId, Customer = customer, Subject = request.Message.Length > 70 ? request.Message[..70] : request.Message, CustomerLanguage = request.Language, Status = "open" };
    var translated = await translator.TranslateAsync(new TranslationRequest(request.Message, request.Language, "en"), ct);
    conversation.Messages.Add(new Message { SenderType = "customer", SenderName = customer.DisplayName, OriginalText = request.Message, OriginalLanguage = request.Language, TranslatedText = translated.TranslatedText, TargetLanguage = "en", TranslationStatus = "completed", DeliveryStatus = "received" });
    db.Conversations.Add(conversation);
    db.UsageEvents.Add(new UsageEvent { WorkspaceId = DemoIds.WorkspaceId, EventKey = $"conversation:{conversation.Id}", Quantity = 1 });
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/conversations/{conversation.Id}", new { conversation.Id, conversation.CustomerLanguage, translation = translated.TranslatedText });
});

api.MapPost("/public/chat/{workspacePublicId:guid}/conversations/{conversationId:guid}/messages", async (Guid workspacePublicId, Guid conversationId, CreateCustomerMessageRequest request, AppDbContext db, ITranslationService translator, CancellationToken ct) =>
{
    if (workspacePublicId != DemoIds.WorkspaceId) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 10000) return Results.BadRequest(new { error = "Message is required and must be 10,000 characters or fewer." });
    var conversation = await db.Conversations.Include(c => c.Customer).FirstOrDefaultAsync(c => c.Id == conversationId && c.WorkspaceId == DemoIds.WorkspaceId, ct);
    if (conversation is null) return Results.NotFound();
    var language = request.Language is not null && LanguageCodes.All.Contains(request.Language) ? request.Language : conversation.CustomerLanguage;
    var translation = await translator.TranslateAsync(new TranslationRequest(request.Text, language, "en"), ct);
    db.Messages.Add(new Message { ConversationId = conversationId, SenderType = "customer", SenderName = conversation.Customer?.DisplayName ?? "Customer", OriginalText = request.Text.Trim(), OriginalLanguage = language, TranslatedText = translation.TranslatedText, TargetLanguage = "en", TranslationStatus = "completed", DeliveryStatus = "received" });
    conversation.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);
    return Results.Created($"/api/v1/conversations/{conversationId}/messages", new { translated = translation.TranslatedText });
});

app.Run();

public static class LanguageCodes
{
    public static readonly string[] All = ["en", "es", "fr", "zh-CN", "de"];
}

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext db)
    {
        if (await db.Workspaces.AnyAsync(w => w.Id == DemoIds.WorkspaceId)) return;
        var workspace = new Workspace { Id = DemoIds.WorkspaceId, Name = "Northstar Commerce", DefaultLanguage = "en" };
        var customers = new[]
        {
            new Customer { WorkspaceId = DemoIds.WorkspaceId, DisplayName = "Sofia Martinez", Email = "sofia@example.com", Language = "es" },
            new Customer { WorkspaceId = DemoIds.WorkspaceId, DisplayName = "Camille Laurent", Email = "camille@example.com", Language = "fr" },
            new Customer { WorkspaceId = DemoIds.WorkspaceId, DisplayName = "Lukas Weber", Email = "lukas@example.com", Language = "de" },
            new Customer { WorkspaceId = DemoIds.WorkspaceId, DisplayName = "Wei Chen", Email = "wei@example.com", Language = "zh-CN" },
            new Customer { WorkspaceId = DemoIds.WorkspaceId, DisplayName = "Emma Wilson", Email = "emma@example.com", Language = "en" }
        };
        db.Workspaces.Add(workspace);
        db.Customers.AddRange(customers);
        var conversations = new[]
        {
            new Conversation { WorkspaceId = DemoIds.WorkspaceId, Customer = customers[0], Subject = "Order delivery update", CustomerLanguage = "es", Status = "open", AssignedAgent = "Alex Morgan" },
            new Conversation { WorkspaceId = DemoIds.WorkspaceId, Customer = customers[1], Subject = "Question about return policy", CustomerLanguage = "fr", Status = "open", AssignedAgent = "Alex Morgan" },
            new Conversation { WorkspaceId = DemoIds.WorkspaceId, Customer = customers[2], Subject = "Package has not arrived", CustomerLanguage = "de", Status = "pending", AssignedAgent = "Jamie Chen" },
            new Conversation { WorkspaceId = DemoIds.WorkspaceId, Customer = customers[3], Subject = "Product availability", CustomerLanguage = "zh-CN", Status = "open", AssignedAgent = "Alex Morgan" },
            new Conversation { WorkspaceId = DemoIds.WorkspaceId, Customer = customers[4], Subject = "Thanks for your help", CustomerLanguage = "en", Status = "resolved", AssignedAgent = "Jamie Chen" }
        };
        db.Conversations.AddRange(conversations);
        db.Messages.AddRange(new[]
        {
            new Message { Conversation = conversations[0], SenderType = "customer", SenderName = customers[0].DisplayName, OriginalText = "Hola, necesito ayuda con mi pedido.", OriginalLanguage = "es", TranslatedText = "Hello, I need help with my order.", TargetLanguage = "en", TranslationStatus = "completed", DeliveryStatus = "received", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-4) },
            new Message { Conversation = conversations[0], SenderType = "agent", SenderName = "Alex Morgan", OriginalText = "Of course. I can check the latest delivery status for you.", OriginalLanguage = "en", TranslatedText = "Por supuesto. Puedo comprobar el estado más reciente de la entrega.", TargetLanguage = "es", TranslationStatus = "completed", DeliveryStatus = "sent", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3) },
            new Message { Conversation = conversations[1], SenderType = "customer", SenderName = customers[1].DisplayName, OriginalText = "Bonjour, quelles sont vos conditions de retour ?", OriginalLanguage = "fr", TranslatedText = "Hello, what is your return policy?", TargetLanguage = "en", TranslationStatus = "completed", DeliveryStatus = "received", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15) },
            new Message { Conversation = conversations[2], SenderType = "customer", SenderName = customers[2].DisplayName, OriginalText = "Meine Bestellung ist noch nicht angekommen.", OriginalLanguage = "de", TranslatedText = "My order has not arrived yet.", TargetLanguage = "en", TranslationStatus = "completed", DeliveryStatus = "received", CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) },
            new Message { Conversation = conversations[3], SenderType = "customer", SenderName = customers[3].DisplayName, OriginalText = "我的订单什么时候可以发货？", OriginalLanguage = "zh-CN", TranslatedText = "When can my order be shipped?", TargetLanguage = "en", TranslationStatus = "completed", DeliveryStatus = "received", CreatedAt = DateTimeOffset.UtcNow.AddHours(-2) },
            new Message { Conversation = conversations[4], SenderType = "customer", SenderName = customers[4].DisplayName, OriginalText = "Thank you, that solved my problem.", OriginalLanguage = "en", TranslatedText = "Thank you, that solved my problem.", TargetLanguage = "en", TranslationStatus = "completed", DeliveryStatus = "received", CreatedAt = DateTimeOffset.UtcNow.AddHours(-4) }
        });
        db.UsageEvents.Add(new UsageEvent { WorkspaceId = DemoIds.WorkspaceId, EventKey = "seed:starter", Quantity = 43, OccurredAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }
}
