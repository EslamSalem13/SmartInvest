namespace SmartInvest.Application.DTOs;

/// <summary>ملف مرفوع (يُبنى في الـ Controller من IFormFile — طبقة Application لا تعرف ASP.NET).</summary>
public class FileUploadDto
{
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public byte[] Content { get; set; } = [];
}

/// <summary>ملف للتحميل.</summary>
public class FileDownloadDto
{
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
}

/// <summary>بيانات ملف داخل إصدار (بدون المحتوى).</summary>
public class ProcurementFileDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileExtension { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

public class ProcurementVersionDto
{
    public int Id { get; set; }
    public int VersionNumber { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ProcurementFileDto> Files { get; set; } = [];
}

/// <summary>خانة ملف متاحة في مرحلة (حتى تعرف الواجهة ماذا ترفع).</summary>
public class ProcurementFileSlotDto
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public bool Required { get; set; }
}

public class ProcurementStageDto
{
    public string Stage { get; set; } = string.Empty;
    public string StageLabel { get; set; } = string.Empty;
    public int Order { get; set; }
    public int? DocumentId { get; set; }
    public int CurrentVersionNumber { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public List<ProcurementFileSlotDto> FileSlots { get; set; } = [];
    public bool IsLocked { get; set; }

    /// <summary>تأكيد صرف الدفعة المقدمة 25% — قيمة غير فارغة فقط لمرحلة "العقد والترسية".</summary>
    public bool? AdvancePaymentDone { get; set; }
}

public class ProcurementStageDetailDto : ProcurementStageDto
{
    public List<ProcurementVersionDto> Versions { get; set; } = [];
}

/// <summary>ملخص مذكرة عرض داخل صفحة المشروع.</summary>
public class PresentationMemoBriefDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CurrentVersionNumber { get; set; }
    public bool IsCompleted { get; set; }
}

public class ProcurementOverviewDto
{
    public int SubProjectId { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string? SubProjectCode { get; set; }
    public List<PresentationMemoBriefDto> PresentationMemos { get; set; } = [];
    public List<ProcurementStageDto> Stages { get; set; } = [];
}

/// <summary>عنصر قائمة "التعاقدات" — مشروع فرعي + تقدم مراحله.</summary>
public class ProcurementSubProjectListItemDto
{
    public int SubProjectId { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string? SubProjectCode { get; set; }
    public string MainProjectName { get; set; } = string.Empty;
    public int CompletedStages { get; set; }
    public int TotalStages { get; set; }
    public bool HasPresentationMemo { get; set; }
}

public class UploadProcurementVersionDto
{
    public string? Notes { get; set; }
    public Dictionary<string, FileUploadDto> Files { get; set; } = [];
}

public class SetCompletionDto
{
    public bool IsCompleted { get; set; }
}

public class SetAdvancePaymentDoneDto
{
    public bool Done { get; set; }
}
