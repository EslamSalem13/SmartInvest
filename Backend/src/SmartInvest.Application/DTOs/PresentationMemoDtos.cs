namespace SmartInvest.Application.DTOs;

public class MemoSubProjectDto
{
    public int SubProjectId { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string? SubProjectCode { get; set; }

    /// <summary>"مقاولات" أو "توريدات" — تصنيف واجهة قائمة مذكرة العرض حسب نوع المشروع.</summary>
    public string? ProjectNature { get; set; }
}

public class PresentationMemoDto
{
    public int Id { get; set; }
    public int? FinancialYearId { get; set; }
    public string? FinancialYearName { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CurrentVersionNumber { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<MemoSubProjectDto> SubProjects { get; set; } = [];

    /// <summary>طريقة التعاقد — رقم من <c>ContractingMethod</c>، فارغ للمذكرات القديمة.</summary>
    public int? ContractingMethod { get; set; }

    /// <summary>التسمية العربية لطريقة التعاقد — للعرض المباشر في القوائم.</summary>
    public string? ContractingMethodLabel { get; set; }
}

public class PresentationMemoDetailDto : PresentationMemoDto
{
    public List<ProcurementVersionDto> Versions { get; set; } = [];
}

public class CreatePresentationMemoDto
{
    public int FinancialYearId { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<int> SubProjectIds { get; set; } = [];
    public int? ContractingMethod { get; set; }
}

public class UpdatePresentationMemoDto
{
    public int FinancialYearId { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<int> SubProjectIds { get; set; } = [];
    public int? ContractingMethod { get; set; }
}

public class UploadMemoVersionDto
{
    public string? Notes { get; set; }
    public FileUploadDto File { get; set; } = new();

    /// <summary>
    /// قرار لجنة الشؤون القانونية — اختياري عند رفع الإصدار، وإلزامي قبل إكمال المذكرة.
    /// يُسمح برفع الإصدار بدونه حتى لا يتعطّل العمل بانتظار القرار.
    /// </summary>
    public FileUploadDto? LegalAffairsCommitteeDecision { get; set; }
}
