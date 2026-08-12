using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartInvest.Domain.Enums;

namespace SmartInvest.Domain.Entities;

public class PlanApprovalNotification
{
    [Key]
    public int PlanApprovalNotificationId { get; set; }

    public int PlanId { get; set; }
    public virtual Plan Plan { get; set; } = null!;

    [MaxLength(80)]
    public string EventType { get; set; } = "PlanApproved";

    public PlanApprovalNotificationStatus Status { get; set; } = PlanApprovalNotificationStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessingStartedAtUtc { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public int AttemptCount { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }

    [MaxLength(300)]
    public string? Subject { get; set; }
    public string? HtmlBody { get; set; }
    public string? PlainTextBody { get; set; }
    public bool AiGenerationUsed { get; set; }

    [MaxLength(450)]
    public string ApprovedByUserId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ApprovedByName { get; set; } = string.Empty;

    public int ProjectCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal BankFunding { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SelfFunding { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AvailableFunding { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<PlanApprovalNotificationRecipient> Recipients { get; set; } =
        new List<PlanApprovalNotificationRecipient>();
}

public class PlanApprovalNotificationRecipient
{
    [Key]
    public int PlanApprovalNotificationRecipientId { get; set; }

    public int NotificationId { get; set; }
    public virtual PlanApprovalNotification Notification { get; set; } = null!;

    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(256)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [MaxLength(80)]
    public string Role { get; set; } = string.Empty;

    public PlanApprovalRecipientStatus Status { get; set; } = PlanApprovalRecipientStatus.Pending;
    public int AttemptCount { get; set; }
    public DateTime? SentAtUtc { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }
}
