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

    // ===== المدة القصوى وزر الفشل =====

    /// <summary>المدة القصوى بالأيام التي حددها مدير التخطيط — null يعني بلا موعد نهائي. لا تُستخدم لمرحلة الإعلان.</summary>
    public int? DurationDays { get; set; }

    /// <summary>
    /// الموعد النهائي — محسوب دائمًا في الخادم، وليس CreatedAt + DurationDays حرفيًا في كل الأحوال:
    /// مرحلة الإعلان تحسبه من AnnouncementDate + 15 يومًا الثابتة بدلًا من DurationDays.
    /// </summary>
    public DateTime? Deadline { get; set; }

    /// <summary>تجاوز الموعد النهائي دون إكمال — الشرط الوحيد لظهور زر الفشل في الواجهة.</summary>
    public bool CanFail { get; set; }

    public bool IsSkipped { get; set; }
    public string? SkipReason { get; set; }

    public DateTime? FailedAt { get; set; }
    public string? FailureReason { get; set; }

    /// <summary>تاريخ نشر الإعلان — غير فارغ فقط لمرحلة "الإعلان".</summary>
    public DateTime? AnnouncementDate { get; set; }
}

public class SetStageDurationDto
{
    public int? DurationDays { get; set; }
}

public class SetAnnouncementDateDto
{
    public DateTime AnnouncementDate { get; set; }
}

public class SkipStageDto
{
    public string Reason { get; set; } = string.Empty;
}

public class FailStageDto
{
    public string Reason { get; set; } = string.Empty;
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

    /// <summary>تاريخ صرف الدفعة المقدمة — يظهر كموعد مرحلة الدفعة المقدمة في متابعة المشروعات.</summary>
    public DateTime? AdvancePaymentDate { get; set; }

    public int? ExecutionDurationMonths { get; set; }
    public int? ExecutionDurationDays { get; set; }
    public int? SiteHandoverMode { get; set; }
    public DateTime? SiteHandoverDate { get; set; }

    /// <summary>اسم ملف إثبات تسليم الأرضية — null يعني لم يُرفع بعد.</summary>
    public string? SiteHandoverProofFileName { get; set; }

    /// <summary>تاريخ التسليم المستحق — محسوب من تاريخ تسليم الأرضية + المدة.</summary>
    public DateTime? ContractualDeliveryDate { get; set; }

    public decimal? PenaltyAmount { get; set; }

    public int? ContractorId { get; set; }
    public string? ContractorName { get; set; }

    /// <summary>
    /// مُشتق من طريقة تعاقد مذكرة العرض الفعّالة — لا يُختار مستقلًا، راجع UpsertAssignmentAsync.
    /// الاسم يُقرأ من overview().activePresentationMemo.contractingMethodLabel في الواجهة (موجود
    /// بالفعل)، لا حاجة لحقل اسم منفصل هنا — الـId وحده كافٍ كسجلّ لما تم إسناده فعليًا.
    /// </summary>
    public int? ContractTypeId { get; set; }

    /// <summary>تاريخ العقد — يحل محل رقم العقد في كل واجهات المستخدم.</summary>
    public DateTime? ContractDate { get; set; }
    public decimal? ContractValue { get; set; }

    /// <summary>الإجمالي المخطط ناقص قيمة العقد — تُعرض فقط عندما تكون القيمة موجبة (قيمة العقد أقل من المخطط).</summary>
    public decimal? Savings { get; set; }
}

/// <summary>حفظ بيانات مرحلة الترسية. كل الحقول اختيارية — تُحفظ تدريجيًا ويُتحقق منها عند الإكمال.</summary>
public class SetContractAwardDetailsDto
{
    public bool AdvancePaymentDone { get; set; }
    public decimal? AdvancePaymentPercentage { get; set; }
    public decimal? AdvancePaymentSelfAmount { get; set; }
    public decimal? AdvancePaymentBankAmount { get; set; }
    public DateTime? AdvancePaymentDate { get; set; }

    public int? ExecutionDurationMonths { get; set; }
    public int? ExecutionDurationDays { get; set; }
    public int? SiteHandoverMode { get; set; }

    public decimal? PenaltyAmount { get; set; }

    public int? ContractorId { get; set; }
    public DateTime? ContractDate { get; set; }
    public decimal? ContractValue { get; set; }
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

    /// <summary>نوع التعاقد المحدد في المذكرة — يظهر جنب اسمها في صفحة مراحل الطرح.</summary>
    public string? ContractingMethodLabel { get; set; }
}

public class ProcurementOverviewDto
{
    public int SubProjectId { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string? SubProjectCode { get; set; }
    /// <summary>مذكرة العرض الفعّالة — الأحدث وحدها، وليست كل المذكرات المرتبطة تاريخيًا.</summary>
    public PresentationMemoBriefDto? ActivePresentationMemo { get; set; }

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

    // ===== مذكرة العرض الفعّالة =====
    // المشروع قد يكون مرتبطًا بأكثر من مذكرة تاريخيًا، لكن الفعّالة هي الأحدث وحدها.

    public int? ActiveMemoId { get; set; }
    public string? ActiveMemoTitle { get; set; }

    /// <summary>نوع التعاقد المأخوذ من المذكرة الفعّالة.</summary>
    public int? ContractingMethod { get; set; }
    public string? ContractingMethodLabel { get; set; }
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
