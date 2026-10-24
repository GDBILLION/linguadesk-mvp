namespace LinguaDesk.Api;

public static class DemoIds
{
    public static readonly Guid WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111");
}

public class Workspace
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Northstar Commerce";
    public string DefaultLanguage { get; set; } = "en";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public string Language { get; set; } = "en";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class Conversation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public Guid CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public string Subject { get; set; } = "";
    public string CustomerLanguage { get; set; } = "en";
    public string Status { get; set; } = "open";
    public string AssignedAgent { get; set; } = "Alex Morgan";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Message> Messages { get; set; } = [];
}

public class Message
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public Conversation? Conversation { get; set; }
    public string SenderType { get; set; } = "customer";
    public string SenderName { get; set; } = "";
    public string OriginalText { get; set; } = "";
    public string OriginalLanguage { get; set; } = "en";
    public string? TranslatedText { get; set; }
    public string TargetLanguage { get; set; } = "en";
    public string TranslationStatus { get; set; } = "not_required";
    public string DeliveryStatus { get; set; } = "received";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public class UsageEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string EventKey { get; set; } = "";
    public int Quantity { get; set; } = 1;
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public record TranslationRequest(string Text, string SourceLanguage, string TargetLanguage);
public record TranslationResponse(string TranslatedText, string Provider, bool IsDemo);
public record SendMessageRequest(string Text, string ApprovedTranslation);
public record UpdateStatusRequest(string Status);
public record CreateConversationRequest(string CustomerName, string? Email, string Language, string Message);
public record CreateCustomerMessageRequest(string Text, string? Language);
