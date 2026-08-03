using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartInvest.Application.Common.Ai;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.Infrastructure.Services;

// Calls Google's Gemini API (generativelanguage.googleapis.com) directly - switched from the
// previous ITI student-tier proxy so measurement extraction and lookup-match suggestions (both
// AI-assisted, both invoked on every import preview) run on Gemini's free tier instead of a
// limited paid credit allowance.
public class AiGatewayClient : IAiGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly AiGatewayOptions _options;
    private readonly ILogger<AiGatewayClient> _logger;

    public AiGatewayClient(HttpClient httpClient, IOptions<AiGatewayOptions> options, ILogger<AiGatewayClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    private record TextPart([property: JsonPropertyName("text")] string Text);

    private record ContentEntry(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("parts")] List<TextPart> Parts);

    private record SystemInstruction([property: JsonPropertyName("parts")] List<TextPart> Parts);

    private record ThinkingConfig([property: JsonPropertyName("thinkingBudget")] int ThinkingBudget);

    private record GenerationConfig(
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens,
        [property: JsonPropertyName("thinkingConfig")] ThinkingConfig ThinkingConfig);

    private record GenerateContentRequest(
        [property: JsonPropertyName("system_instruction")] SystemInstruction SystemInstruction,
        [property: JsonPropertyName("contents")] List<ContentEntry> Contents,
        [property: JsonPropertyName("generationConfig")] GenerationConfig GenerationConfig);

    private record ResponseContent([property: JsonPropertyName("parts")] List<TextPart>? Parts);
    private record Candidate([property: JsonPropertyName("content")] ResponseContent? Content);
    private record GenerateContentResponse([property: JsonPropertyName("candidates")] List<Candidate>? Candidates);

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("AI gateway call skipped: API key is not configured");
            throw new BusinessRuleException("مفتاح خدمة الذكاء الاصطناعي غير مُهيأ");
        }

        // thinkingBudget must be >= 1 for this model (0 is rejected as invalid) - kept at the
        // floor so as little of maxTokens as possible goes to internal reasoning instead of the
        // actual answer. Even at the floor, thinking still consumes a meaningful share of the
        // budget, which is why callers pass generous maxTokens values.
        var request = new GenerateContentRequest(
            new SystemInstruction(new List<TextPart> { new(systemPrompt) }),
            new List<ContentEntry> { new("user", new List<TextPart> { new(userMessage) }) },
            new GenerationConfig(maxTokens, new ThinkingConfig(1)));

        var url = $"{_options.BaseUrl}/{_options.ModelId}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "AI gateway call failed to connect or timed out");
            throw new BusinessRuleException($"تعذّر الاتصال بخدمة الذكاء الاصطناعي: {ex.Message}");
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("AI gateway call failed: {StatusCode} {Body}", httpResponse.StatusCode, body);
            throw new BusinessRuleException($"فشل طلب الذكاء الاصطناعي (رمز الحالة: {(int)httpResponse.StatusCode})");
        }

        GenerateContentResponse? parsed;
        try
        {
            parsed = await httpResponse.Content.ReadFromJsonAsync<GenerateContentResponse>(cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI gateway response JSON parse failure");
            throw new BusinessRuleException($"تعذّر قراءة رد خدمة الذكاء الاصطناعي: {ex.Message}");
        }

        var outputText = parsed?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (outputText == null)
        {
            _logger.LogWarning("AI gateway response was empty");
        }

        return outputText ?? throw new BusinessRuleException("رد خدمة الذكاء الاصطناعي فارغ");
    }
}
