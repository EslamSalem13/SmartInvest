namespace SmartInvest.Domain.Common;

/// <summary>
/// أساس إصدارات المستندات. الإصدار سجل تاريخي غير قابل للتعديل أو الحذف —
/// أي تغيير يعني إنشاء إصدار جديد برقم أعلى.
/// </summary>
public abstract class DocumentVersionBase : BaseEntity
{
    public int VersionNumber { get; set; }

    public string? Notes { get; set; }
}
