using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartInvest.Application.Common.Ai;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.Infrastructure.Services;

/// <summary>
/// نقطة الاتصال الوحيدة بأي خدمة ذكاء اصطناعي في التطبيق. تدعم 4 مزوّدين (AiGatewayOptions.Provider):
/// ITI (بروكسي وسيط)، أو استدعاء مباشر لواجهة Anthropic / Gemini / OpenAI الرسمية.
/// كل الخدمات الأخرى (MeasurementExtractionService، LookupMatchSuggestionService...) تستدعي
/// CompleteAsync فقط ولا تعرف أي شيء عن المزوّد الفعلي — تغيير المزوّد أو المفتاح أو الموديل
/// يتم بالكامل من appsettings.Local.json بدون أي تعديل كود.
/// </summary>
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

    public Task<string> CompleteAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken cancellationToken = default)
    {
        return CompleteAsync(systemPrompt, userMessage, maxTokens, AiWorkload.Default, cancellationToken);
    }

    public Task<string> CompleteAsync(
        string systemPrompt,
        string userMessage,
        int maxTokens,
        AiWorkload workload,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("AI gateway call skipped: API key is not configured");
            throw new BusinessRuleException("مفتاح خدمة الذكاء الاصطناعي غير مُهيأ");
        }

        var modelId = ResolveModelId(workload);

        return _options.Provider switch
        {
            AiProvider.Iti => CompleteViaItiAsync(systemPrompt, userMessage, maxTokens, modelId, cancellationToken),
            AiProvider.Anthropic => CompleteViaAnthropicAsync(systemPrompt, userMessage, maxTokens, modelId, cancellationToken),
            AiProvider.Gemini => CompleteViaGeminiAsync(systemPrompt, userMessage, maxTokens, modelId, cancellationToken),
            AiProvider.OpenAi => CompleteViaOpenAiAsync(systemPrompt, userMessage, maxTokens, modelId, cancellationToken),
            _ => throw new BusinessRuleException($"مزوّد الذكاء الاصطناعي «{_options.Provider}» غير مدعوم"),
        };
    }

    private string ResolveModelId(AiWorkload workload)
    {
        var workloadModelId = workload switch
        {
            AiWorkload.ExcelImport => _options.ExcelImportModelId,
            AiWorkload.Reports => _options.ReportsModelId,
            AiWorkload.PlanApprovalEmail => _options.PlanApprovalEmailModelId,
            _ => _options.ModelId,
        };
        var modelId = string.IsNullOrWhiteSpace(workloadModelId) ? _options.ModelId : workloadModelId;
        if (string.IsNullOrWhiteSpace(modelId))
        {
            throw new BusinessRuleException("موديل خدمة الذكاء الاصطناعي غير مُهيأ لهذه العملية");
        }

        return modelId.Trim();
    }

    // ===== ITI (بروكسي وسيط للطلاب) — /student/chat بصيغة مخصصة =====
    private record ItiChatMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);

    private record ItiChatRequest(
        [property: JsonPropertyName("model_id")] string ModelId,
        [property: JsonPropertyName("messages")] List<ItiChatMessage> Messages,
        [property: JsonPropertyName("system_prompt")] string SystemPrompt,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private record ItiChatResponse([property: JsonPropertyName("output_text")] string? OutputText);

    private async Task<string> CompleteViaItiAsync(string systemPrompt, string userMessage, int maxTokens, string modelId, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? "http://apiaccess.iti.net.eg/api/v1" : _options.BaseUrl;
        var request = new ItiChatRequest(modelId, new List<ItiChatMessage> { new("user", userMessage) }, systemPrompt, maxTokens);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/student/chat")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var httpResponse = await SendAsync(httpRequest, cancellationToken);
        var parsed = await ReadJsonAsync<ItiChatResponse>(httpResponse, cancellationToken);
        return parsed?.OutputText ?? throw new BusinessRuleException("رد خدمة الذكاء الاصطناعي فارغ");
    }

    // ===== Anthropic الرسمية المباشرة (Messages API) =====
    private record AnthropicMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);

    private record AnthropicRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] List<AnthropicMessage> Messages);

    private record AnthropicContentBlock([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("text")] string? Text);
    private record AnthropicResponse([property: JsonPropertyName("content")] List<AnthropicContentBlock>? Content);

    private async Task<string> CompleteViaAnthropicAsync(string systemPrompt, string userMessage, int maxTokens, string modelId, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? "https://api.anthropic.com/v1" : _options.BaseUrl;
        var request = new AnthropicRequest(modelId, maxTokens, systemPrompt, new List<AnthropicMessage> { new("user", userMessage) });

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/messages")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Add("x-api-key", _options.ApiKey);
        httpRequest.Headers.Add("anthropic-version", "2023-06-01");

        var httpResponse = await SendAsync(httpRequest, cancellationToken);
        var parsed = await ReadJsonAsync<AnthropicResponse>(httpResponse, cancellationToken);
        var text = parsed?.Content?.FirstOrDefault(b => b.Type == "text")?.Text;
        return text ?? throw new BusinessRuleException("رد خدمة الذكاء الاصطناعي فارغ");
    }

    // ===== Google Gemini الرسمية المباشرة (generateContent) =====
    private record GeminiTextPart([property: JsonPropertyName("text")] string Text);
    private record GeminiContentEntry([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("parts")] List<GeminiTextPart> Parts);
    private record GeminiSystemInstruction([property: JsonPropertyName("parts")] List<GeminiTextPart> Parts);
    private record GeminiThinkingConfig([property: JsonPropertyName("thinkingBudget")] int ThinkingBudget);

    private record GeminiGenerationConfig(
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens,
        [property: JsonPropertyName("thinkingConfig")] GeminiThinkingConfig ThinkingConfig);

    private record GeminiRequest(
        [property: JsonPropertyName("system_instruction")] GeminiSystemInstruction SystemInstruction,
        [property: JsonPropertyName("contents")] List<GeminiContentEntry> Contents,
        [property: JsonPropertyName("generationConfig")] GeminiGenerationConfig GenerationConfig);

    private record GeminiResponseContent([property: JsonPropertyName("parts")] List<GeminiTextPart>? Parts);
    private record GeminiCandidate([property: JsonPropertyName("content")] GeminiResponseContent? Content);
    private record GeminiResponse([property: JsonPropertyName("candidates")] List<GeminiCandidate>? Candidates);

    private async Task<string> CompleteViaGeminiAsync(string systemPrompt, string userMessage, int maxTokens, string modelId, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? "https://generativelanguage.googleapis.com/v1beta/models" : _options.BaseUrl;

        // thinkingBudget must be >= 1 for Gemini's "thinking" models (0 is rejected as invalid) -
        // kept at the floor so as little of maxTokens as possible goes to internal reasoning.
        var request = new GeminiRequest(
            new GeminiSystemInstruction(new List<GeminiTextPart> { new(systemPrompt) }),
            new List<GeminiContentEntry> { new("user", new List<GeminiTextPart> { new(userMessage) }) },
            new GeminiGenerationConfig(maxTokens, new GeminiThinkingConfig(1)));

        var url = $"{baseUrl}/{modelId}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(request) };

        var httpResponse = await SendAsync(httpRequest, cancellationToken);
        var parsed = await ReadJsonAsync<GeminiResponse>(httpResponse, cancellationToken);
        var text = parsed?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        return text ?? throw new BusinessRuleException("رد خدمة الذكاء الاصطناعي فارغ");
    }

    // ===== OpenAI الرسمية المباشرة (Chat Completions) =====
    private record OpenAiMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);

    private record OpenAiRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("messages")] List<OpenAiMessage> Messages);

    private record OpenAiResponseMessage([property: JsonPropertyName("content")] string? Content);
    private record OpenAiChoice([property: JsonPropertyName("message")] OpenAiResponseMessage? Message);
    private record OpenAiResponse([property: JsonPropertyName("choices")] List<OpenAiChoice>? Choices);

    private async Task<string> CompleteViaOpenAiAsync(string systemPrompt, string userMessage, int maxTokens, string modelId, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl) ? "https://api.openai.com/v1" : _options.BaseUrl;
        var messages = new List<OpenAiMessage> { new("system", systemPrompt), new("user", userMessage) };
        var request = new OpenAiRequest(modelId, maxTokens, messages);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        var httpResponse = await SendAsync(httpRequest, cancellationToken);
        var parsed = await ReadJsonAsync<OpenAiResponse>(httpResponse, cancellationToken);
        var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content;
        return text ?? throw new BusinessRuleException("رد خدمة الذكاء الاصطناعي فارغ");
    }

    // ===== معالجة موحّدة للأخطاء عبر كل المزوّدين =====
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.SendAsync(request, cancellationToken);
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

        return httpResponse;
    }

    private async Task<T?> ReadJsonAsync<T>(HttpResponseMessage httpResponse, CancellationToken cancellationToken)
    {
        try
        {
            return await httpResponse.Content.ReadFromJsonAsync<T>(cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI gateway response JSON parse failure");
            throw new BusinessRuleException($"تعذّر قراءة رد خدمة الذكاء الاصطناعي: {ex.Message}");
        }
    }
}
