using System.Net.Http.Json;

namespace LinguaDesk.Api;

public interface ITranslationService
{
    Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken);
}

public class DemoTranslationService : ITranslationService
{
    private static readonly Dictionary<(string, string, string), string> Samples = new()
    {
        [("en", "es", "Hello, how can I help you today?")] = "Hola, ¿cómo puedo ayudarte hoy?",
        [("en", "fr", "Hello, how can I help you today?")] = "Bonjour, comment puis-je vous aider aujourd’hui ?",
        [("en", "de", "Hello, how can I help you today?")] = "Hallo, wie kann ich Ihnen heute helfen?",
        [("en", "zh-CN", "Hello, how can I help you today?")] = "您好，今天有什么可以帮您？",
        [("es", "en", "Hola, necesito ayuda con mi pedido.")] = "Hello, I need help with my order.",
        [("fr", "en", "Bonjour, où est ma commande ?")] = "Hello, where is my order?",
        [("de", "en", "Meine Bestellung ist noch nicht angekommen.")] = "My order has not arrived yet.",
        [("zh-CN", "en", "我的订单在哪里？")] = "Where is my order?"
    };

    public Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        var text = request.Text.Trim();
        if (Samples.TryGetValue((request.SourceLanguage, request.TargetLanguage, text), out var known))
            return Task.FromResult(new TranslationResponse(known, "demo", true));

        // Deliberately obvious fallback: never pretend this is a real translation.
        var result = $"[DEMO TRANSLATION {request.SourceLanguage} → {request.TargetLanguage}] {text}";
        return Task.FromResult(new TranslationResponse(result, "demo", true));
    }
}

public class LibreTranslateService(HttpClient http, IConfiguration configuration) : ITranslationService
{
    public async Task<TranslationResponse> TranslateAsync(TranslationRequest request, CancellationToken cancellationToken)
    {
        var url = configuration["Translation:LibreTranslateUrl"];
        if (string.IsNullOrWhiteSpace(url))
            throw new InvalidOperationException("Translation:LibreTranslateUrl is required for LibreTranslate provider.");

        var body = new Dictionary<string, object?>
        {
            ["q"] = request.Text,
            ["source"] = request.SourceLanguage == "zh-CN" ? "zh" : request.SourceLanguage,
            ["target"] = request.TargetLanguage == "zh-CN" ? "zh" : request.TargetLanguage,
            ["format"] = "text"
        };
        var apiKey = configuration["Translation:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey)) body["api_key"] = apiKey;

        using var response = await http.PostAsJsonAsync(url, body, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<LibreTranslateResponse>(cancellationToken: cancellationToken);
        if (string.IsNullOrWhiteSpace(result?.TranslatedText))
            throw new InvalidOperationException("Translation provider returned an empty translation.");

        return new TranslationResponse(result.TranslatedText, "libretranslate", false);
    }

    private sealed class LibreTranslateResponse
    {
        public string TranslatedText { get; set; } = "";
    }
}
