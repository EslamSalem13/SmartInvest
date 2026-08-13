namespace SmartInvest.Domain.Common;

/// <summary>
/// أساس مستندات التعاقدات المرتبطة بمشروع فرعي واحد (علاقة 1:1).
/// مذكرة العرض مستثناة — علاقتها M:N.
/// </summary>
public abstract class SubProjectDocumentBase : DocumentBase
{
    public int SubProjectId { get; set; }

    // ===== المدة القصوى وزر الفشل =====
    // مدير التخطيط يحدد مدة قصوى للمرحلة (null = بلا موعد نهائي، الزر لا يظهر أبدًا).
    // الموعد النهائي يُحسب من DurationSetAt + DurationDays — لا يُخزَّن، ولا يبدأ قبل تفعيل المرحلة فعليًا.
    // مرحلة الإعلان استثناء: تتجاهل هذا الحقل وتستخدم قاعدة الـ15 يومًا الثابتة من AnnouncementDate.

    /// <summary>المدة القصوى بالأيام قبل أن يظهر زر الفشل — يحددها مدير التخطيط، وتُتجاهَل لمرحلة الإعلان.</summary>
    public int? DurationDays { get; set; }

    /// <summary>وقت تفعيل المرحلة أو آخر إعادة تحديد للمدة القصوى. null يعني أن بداية حقيقية لم تُحدَّد بعد، لذلك لا يُحسب تأخير.</summary>
    public DateTime? DurationSetAt { get; set; }

    // ===== "هذه المرحلة غير لازمة للطرح" =====

    public bool IsSkipped { get; set; }
    public string? SkipReason { get; set; }
    public DateTime? SkippedAt { get; set; }

    // ===== الفشل وإعادة الطرح =====
    // الفشل لا يحذف شيئًا — الإصدارات القديمة سجل تاريخي دائم. فقط يُبطل اكتمال هذه المرحلة
    // وما بعدها (بنفس آلية إعادة الفتح)، ويُسجَّل السبب هنا كأثر تاريخي دائم حتى لو نجحت المحاولة التالية.

    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }
}
