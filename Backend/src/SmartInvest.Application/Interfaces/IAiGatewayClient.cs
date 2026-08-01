namespace SmartInvest.Application.Interfaces;

public interface IAiGatewayClient
{
    Task<string> CompleteAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken cancellationToken = default);
}
