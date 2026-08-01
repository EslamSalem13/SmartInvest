namespace SmartInvest.Domain.Common;

/// <summary>
/// أساس مستندات التعاقدات المرتبطة بمشروع فرعي واحد (علاقة 1:1).
/// مذكرة العرض مستثناة — علاقتها M:N.
/// </summary>
public abstract class SubProjectDocumentBase : DocumentBase
{
    public int SubProjectId { get; set; }
}
