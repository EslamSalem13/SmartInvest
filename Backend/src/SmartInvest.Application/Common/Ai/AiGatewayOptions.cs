namespace SmartInvest.Application.Common.Ai;

public class AiGatewayOptions
{
    public const string SectionName = "AiGateway";

    public string BaseUrl { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
