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

    private record ChatMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);

    private record ChatRequest(
        [property: JsonPropertyName("model_id")] string ModelId,
        [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
        [property: JsonPropertyName("system_prompt")] string SystemPrompt,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private record ChatResponse([property: JsonPropertyName("output_text")] string? OutputText);

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning("AI gateway call skipped: API key is not configured");
            throw new BusinessRuleException("مفتاح خدمة الذكاء الاصطناعي غير مُهيأ");
        }

        var request = new ChatRequest(_options.ModelId, new List<ChatMessage> { new("user", userMessage) }, systemPrompt, maxTokens);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/student/chat")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            _logger.LogWarning(ex, "AI gateway call failed to connect or timed out");
            throw new BusinessRuleException($"تعذّر الاتصال بخدمة الذكاء الاصطناعي: {ex.Message}");
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("AI gateway call failed: {StatusCode}", httpResponse.StatusCode);
            throw new BusinessRuleException($"فشل طلب الذكاء الاصطناعي (رمز الحالة: {(int)httpResponse.StatusCode})");
        }

        ChatResponse? parsed;
        try
        {
            parsed = await httpResponse.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "AI gateway response JSON parse failure");
            throw new BusinessRuleException($"تعذّر قراءة رد خدمة الذكاء الاصطناعي: {ex.Message}");
        }

        if (parsed?.OutputText == null)
        {
            _logger.LogWarning("AI gateway response was empty");
        }

        return parsed?.OutputText ?? throw new BusinessRuleException("رد خدمة الذكاء الاصطناعي فارغ");
    }
}
