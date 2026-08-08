namespace SmartInvest.Application.DTOs;

public class MemoSubProjectDto
{
    public int SubProjectId { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public string? SubProjectCode { get; set; }
}

public class PresentationMemoDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CurrentVersionNumber { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<MemoSubProjectDto> SubProjects { get; set; } = [];
}

public class PresentationMemoDetailDto : PresentationMemoDto
{
    public List<ProcurementVersionDto> Versions { get; set; } = [];
}

public class CreatePresentationMemoDto
{
    public string Title { get; set; } = string.Empty;
    public List<int> SubProjectIds { get; set; } = [];
}

public class UpdatePresentationMemoDto
{
    public string Title { get; set; } = string.Empty;
    public List<int> SubProjectIds { get; set; } = [];
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
