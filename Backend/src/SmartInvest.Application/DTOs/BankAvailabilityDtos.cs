namespace SmartInvest.Application.DTOs;

public class BankAvailabilityDocumentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
}

public class BankAvailabilityDto
{
    public int Id { get; set; }
    public int FinancialYearId { get; set; }
    public decimal Amount { get; set; }
    public DateTime ReceivedDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
    public List<BankAvailabilityDocumentDto> Documents { get; set; } = new();
}

public class BankAvailabilityListDto
{
    public decimal TotalAvailable { get; set; }
    public decimal TotalBankFunding { get; set; }
    public decimal RemainingAvailable { get; set; }
    public IReadOnlyList<BankAvailabilityDto> Items { get; set; } = new List<BankAvailabilityDto>();
}

/// <summary>يُبنى في الـ Controller من multipart/form-data (حقول + حتى 5 مستندات إثبات) — نفس نمط CreateExecutionStageDto.</summary>
public class CreateBankAvailabilityDto
{
    public decimal Amount { get; set; }
    public DateTime ReceivedDate { get; set; }
    public string? Notes { get; set; }
    public List<FileUploadDto> Documents { get; set; } = new();
}

/// <summary>يُبنى في الـ Controller من multipart/form-data — نفس نمط CreateBankAvailabilityDto، مع معرفات المستندات الحالية المطلوب الاحتفاظ بها وأي مستندات جديدة.</summary>
public class UpdateBankAvailabilityDto
{
    public decimal Amount { get; set; }
    public DateTime ReceivedDate { get; set; }
    public string? Notes { get; set; }
    public List<int> KeepDocumentIds { get; set; } = new();
    public List<FileUploadDto> NewDocuments { get; set; } = new();
}
