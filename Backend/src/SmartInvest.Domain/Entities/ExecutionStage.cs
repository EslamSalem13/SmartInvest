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

    /// <summary>دورة التنفيذ المالية. null فقط للمراحل القديمة ذات الملكية الزمنية الغامضة.</summary>
    public int? SubProjectFinancialYearId { get; set; }
    public virtual SubProjectFinancialYear? SubProjectFinancialYear { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// الموعد الابتدائي للمرحلة. null للمراحل المسجَّلة قبل إضافة الحقل، وللمراحل التلقائية
    /// التي لم يتوفر لها تاريخ بداية حقيقي بعد (التسليم الأولي قبل تسليم الأرضية).
    /// </summary>
    public DateTime? StartDate { get; set; }

    public DateTime? Deadline { get; set; }

    /// <summary>
    /// مرحلة التسليم النهائي المُدارة تلقائيًا — تُنشأ عند إكمال الترسية ويُعاد حساب موعدها
    /// من تاريخ تسليم الأرضية + مدة التنفيذ. لا يملك المستخدم إنشاءها أو تعديل موعدها.
    /// Deadline تكون null طالما لم تُسلَّم الأرضية بعد (لا يوجد تاريخ حقيقي يُحسب منه).
    /// </summary>
    public bool IsFinalDelivery { get; set; }

    /// <summary>
    /// مرحلة الدفعة المقدمة المُدارة تلقائيًا — تُنشأ عند تأكيد صرف الدفعة المقدمة في العقد والترسية.
    /// صفٌّ للعرض فقط: مصدر الحقيقة لقيمتها وتاريخها وإثبات صرفها هو <see cref="ContractAward"/>،
    /// لذلك تُستبعَد قيمها من أي مجموع مراحل حتى لا تُحتسب الدفعة مرتين.
    /// </summary>
    public bool IsAdvancePayment { get; set; }

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
