namespace SmartInvest.Application.DTOs;

public class SubProjectFinancialYearDto
{
    public int Id { get; set; }
    public int FinancialYearId { get; set; }
    public string FinancialYearName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsClosed { get; set; }
}

public class LinkFinancialYearDto
{
    public int FinancialYearId { get; set; }
}
