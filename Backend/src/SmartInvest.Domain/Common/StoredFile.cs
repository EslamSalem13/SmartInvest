namespace SmartInvest.Domain.Common;

/// <summary>
/// ملف رسمي مخزَّن داخل قاعدة البيانات (بدل مسار على القرص).
/// يُستخدم كـ Owned Type داخل إصدارات مستندات الإدارة المالية.
/// </summary>
public class StoredFile
{
    public string FileName { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;

    public long FileSize { get; set; }

    public byte[] Content { get; set; } = [];
}
