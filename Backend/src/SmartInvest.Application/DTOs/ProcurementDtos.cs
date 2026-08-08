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

    /// <summary>تاريخ رفع قرار لجنة الشؤون القانونية — إصدارات مذكرة العرض فقط.</summary>
    public DateTime? LegalAffairsDecisionUploadedAt { get; set; }
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

    /// <summary>تأكيد صرف الدفعة المقدمة — قيمة غير فارغة فقط لمرحلة "العقد والترسية".</summary>
    public bool? AdvancePaymentDone { get; set; }

    /// <summary>تفاصيل الترسية — غير فارغة فقط لمرحلة "العقد والترسية".</summary>
    public ContractAwardDetailsDto? ContractAward { get; set; }
}

/// <summary>بيانات مرحلة الترسية بخلاف الملفات: الإسناد، الدفعة المقدمة، المدة، الشرط الجزائي.</summary>
public class ContractAwardDetailsDto
{
    // نوع المشروع يحدد سلوك الدفعة المقدمة: «توريدات» لا دفعة مقدمة لها إطلاقًا
    public string ProjectNature { get; set; } = string.Empty;
    public bool RequiresAdvancePayment { get; set; }

    // ميزانية المشروع — تُعرض من البداية حتى يرى الموظف من أين يصرف
    public decimal TotalCost { get; set; }
    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }

    public bool AdvancePaymentDone { get; set; }
    public decimal? AdvancePaymentPercentage { get; set; }
    public decimal? AdvancePaymentSelfAmount { get; set; }
    public decimal? AdvancePaymentBankAmount { get; set; }

    public int? ExecutionDurationMonths { get; set; }
    public int? ExecutionDurationDays { get; set; }
    public int? SiteHandoverMode { get; set; }
    public DateTime? SiteHandoverDate { get; set; }

    /// <summary>تاريخ التسليم المستحق — محسوب من تاريخ تسليم الأرضية + المدة.</summary>
    public DateTime? ContractualDeliveryDate { get; set; }

    public decimal? PenaltyAmount { get; set; }

    public int? ContractorId { get; set; }
    public string? ContractorName { get; set; }
    public int? ContractTypeId { get; set; }
    public string? ContractNumber { get; set; }
    public decimal? ContractValue { get; set; }
}

/// <summary>حفظ بيانات مرحلة الترسية. كل الحقول اختيارية — تُحفظ تدريجيًا ويُتحقق منها عند الإكمال.</summary>
public class SetContractAwardDetailsDto
{
    public bool AdvancePaymentDone { get; set; }
    public decimal? AdvancePaymentPercentage { get; set; }
    public decimal? AdvancePaymentSelfAmount { get; set; }
    public decimal? AdvancePaymentBankAmount { get; set; }

    public int? ExecutionDurationMonths { get; set; }
    public int? ExecutionDurationDays { get; set; }
    public int? SiteHandoverMode { get; set; }

    public decimal? PenaltyAmount { get; set; }

    public int? ContractorId { get; set; }
    public int? ContractTypeId { get; set; }
    public string? ContractNumber { get; set; }
    public decimal? ContractValue { get; set; }
}

/// <summary>تسجيل تسليم أرضية المشروع للمقاول — تبدأ عندها مدة التنفيذ.</summary>
public class SetSiteHandoverDto
{
    public DateTime HandoverDate { get; set; }
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
