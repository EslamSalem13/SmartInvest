namespace SmartInvest.Application.DTOs;

public class ReportCatalogItemDto
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> IncludedFields { get; set; } = new();
}

public class GenerateAiReportDto
{
    public string Prompt { get; set; } = string.Empty;
    public int? FinancialYearId { get; set; }
}
