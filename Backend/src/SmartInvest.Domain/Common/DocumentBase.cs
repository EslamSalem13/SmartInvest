namespace SmartInvest.Domain.Common;

/// <summary>
/// أساس مستندات دورة التعاقدات (الإدارة المالية).
/// كل مستند له رقم إصدار حالي وحالة اكتمال، والإصدارات القديمة لا تُعدَّل ولا تُحذف أبدًا.
/// </summary>
public abstract class DocumentBase : BaseEntity
{
    /// <summary>رقم أحدث إصدار (0 = لا توجد إصدارات بعد).</summary>
    public int CurrentVersionNumber { get; set; }

    /// <summary>هل اكتملت هذه المرحلة رسميًا؟</summary>
    public bool IsCompleted { get; set; }
}
