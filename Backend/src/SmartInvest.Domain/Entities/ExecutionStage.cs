using SmartInvest.Domain.Common;

namespace SmartInvest.Domain.Entities;

/// <summary>
/// مرحلة تنفيذ حرة يضيفها مدير التخطيط بعد اكتمال العقد والترسية — منفصلة عمدًا عن
/// Entities/Procurement (تلك مراحل الطرح الثابتة الستة قبل الترسية؛ هذه قائمة مفتوحة
/// بعد الترسية، اسم كل مرحلة يكتبه الموظف بنفسه).
/// </summary>
public class ExecutionStage
{
    public int ExecutionStageId { get; set; }

    public int SubProjectId { get; set; }
    public virtual SubProject SubProject { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public DateTime Deadline { get; set; }

    public decimal SelfFundingSpent { get; set; }
    public decimal BankFundingSpent { get; set; }
    public StoredFile? SelfFundingProofFile { get; set; }
    public StoredFile? BankFundingProofFile { get; set; }

    public decimal PhysicalProgressPercent { get; set; }
    public StoredFile? PhysicalProgressProofFile { get; set; }

    public string? Notes { get; set; }

    /// <summary>يُملأ يدويًا عند تجاوز الموعد النهائي — لا يُحسب تلقائيًا.</summary>
    public decimal? PenaltyAmount { get; set; }
    public bool PenaltyPaid { get; set; }

    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
