namespace SmartInvest.Application.Common;

public class PlanApprovalNotificationOptions
{
    public const string SectionName = "PlanApprovalNotifications";

    public bool Enabled { get; set; } = true;
    public int PollingIntervalSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
    public int InitialRetryDelaySeconds { get; set; } = 30;
    public int ProcessingLeaseSeconds { get; set; } = 120;
}
