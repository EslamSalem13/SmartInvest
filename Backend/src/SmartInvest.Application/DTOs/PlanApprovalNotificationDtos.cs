using SmartInvest.Domain.Enums;

namespace SmartInvest.Application.DTOs;

public class PlanApprovalNotificationListItemDto
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string FinancialYearName { get; set; } = string.Empty;
    public PlanApprovalNotificationStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public int RecipientCount { get; set; }
    public int SentCount { get; set; }
    public int FailedCount { get; set; }
    public string? LastError { get; set; }
}

public class PlanApprovalNotificationDetailDto : PlanApprovalNotificationListItemDto
{
    public string ApprovedByName { get; set; } = string.Empty;
    public int ProjectCount { get; set; }
    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }
    public decimal AvailableFunding { get; set; }
    public bool AiGenerationUsed { get; set; }
    public string? Subject { get; set; }
    public IReadOnlyList<PlanApprovalNotificationRecipientDto> Recipients { get; set; } = [];
}

public class PlanApprovalNotificationRecipientDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public PlanApprovalRecipientStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public string? LastError { get; set; }
}
