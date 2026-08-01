namespace SmartInvest.Application.Services.Import;

public enum ImportMode
{
    Suggested,
    Approved,
}

public class ParsedImportRow
{
    public int RowIndex { get; set; }
    public string MainProgramName { get; set; } = string.Empty;
    public string SubProgramName { get; set; } = string.Empty;
    public string MainProjectCode { get; set; } = string.Empty;
    public string MainProjectName { get; set; } = string.Empty;
    public string ProjectLevelName { get; set; } = string.Empty;
    public string ExecutiveAgencyName { get; set; } = string.Empty;
    public string MarkazName { get; set; } = string.Empty;
    public string SubProjectCode { get; set; } = string.Empty;
    public string SubProjectName { get; set; } = string.Empty;
    public string ComponentTypeName { get; set; } = string.Empty;
    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }
    public string AccountingUnitName { get; set; } = string.Empty;
}

public class ParsedImportFile
{
    public ImportMode Mode { get; set; }
    public List<ParsedImportRow> Rows { get; set; } = new();
}
