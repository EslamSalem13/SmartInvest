namespace SmartInvest.Domain.Enums;

public enum PlanApprovalNotificationStatus
{
    Pending,
    Processing,
    Sent,
    PartiallyFailed,
    Failed,
    NoRecipients,
}

public enum PlanApprovalRecipientStatus
{
    Pending,
    Sent,
    Failed,
}
