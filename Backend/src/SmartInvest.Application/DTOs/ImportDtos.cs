namespace SmartInvest.Application.DTOs;

public class UnresolvedNameDto
{
    public string Name { get; set; } = string.Empty;
    public int RowCount { get; set; }
}

public class MainProjectCodeConflictOptionDto
{
    public string MainProjectName { get; set; } = string.Empty;
    public string MainProgramName { get; set; } = string.Empty;
}

public class MainProjectCodeConflictDto
{
    public string Code { get; set; } = string.Empty;
    public List<MainProjectCodeConflictOptionDto> Options { get; set; } = new();
}

public class SuggestedImportPreviewDto
{
    public int MainProjectCount { get; set; }
    public int SubProjectCount { get; set; }
    public List<UnresolvedNameDto> UnresolvedMarkaz { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedMainPrograms { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedSubPrograms { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedAgencies { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedProjectLevels { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedComponentTypes { get; set; } = new();
    public List<UnresolvedNameDto> UnresolvedAccountingUnits { get; set; } = new();
    public List<MainProjectCodeConflictDto> MainProjectCodeConflicts { get; set; } = new();
}

public class UnresolvedImportRowDto
{
    public int RowIndex { get; set; }
    public string MainProjectName { get; set; } = string.Empty;
    public string SubProjectName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ApprovedImportPreviewDto
{
    public int MatchedCount { get; set; }
    public List<UnresolvedImportRowDto> UnresolvedRows { get; set; } = new();
}

public class ExtractedMeasurementDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class RowMeasurementPreviewDto
{
    public int RowIndex { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public List<ExtractedMeasurementDto> Measurements { get; set; } = new();
}

public class ImportPreviewResultDto
{
    public string ImportId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public SuggestedImportPreviewDto? Suggested { get; set; }
    public ApprovedImportPreviewDto? Approved { get; set; }
    public List<RowMeasurementPreviewDto> RowMeasurements { get; set; } = new();
}

public class ImportResolutionDto
{
    public string Name { get; set; } = string.Empty;
    public bool CreateNew { get; set; }
    public int? ExistingId { get; set; }
}

public class MainProjectCodeResolutionDto
{
    public string Code { get; set; } = string.Empty;
    public string ChosenMainProjectName { get; set; } = string.Empty;
    public string ChosenMainProgramName { get; set; } = string.Empty;
}

public class ImportRowResolutionDto
{
    public int RowIndex { get; set; }
    public bool CreateNew { get; set; }
    public int? ExistingSubProjectId { get; set; }
}

public class RowMeasurementResolutionDto
{
    public int RowIndex { get; set; }
    public List<ExtractedMeasurementDto> Measurements { get; set; } = new();
}

public class ImportCommitDto
{
    public string ImportId { get; set; } = string.Empty;
    public int FinancialYearId { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public List<ImportResolutionDto> MarkazResolutions { get; set; } = new();
    public List<ImportResolutionDto> MainProgramResolutions { get; set; } = new();
    public List<ImportResolutionDto> SubProgramResolutions { get; set; } = new();
    public List<ImportResolutionDto> AgencyResolutions { get; set; } = new();
    public List<ImportResolutionDto> ProjectLevelResolutions { get; set; } = new();
    public List<ImportResolutionDto> ComponentTypeResolutions { get; set; } = new();
    public List<ImportResolutionDto> AccountingUnitResolutions { get; set; } = new();
    public List<MainProjectCodeResolutionDto> MainProjectCodeResolutions { get; set; } = new();
    public List<ImportRowResolutionDto> RowResolutions { get; set; } = new();
    public List<RowMeasurementResolutionDto> MeasurementResolutions { get; set; } = new();
}

public class ImportRowFailureDto
{
    public string Name { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}

public class ImportCommitResultDto
{
    public string Mode { get; set; } = string.Empty;
    public int MainProjectsCreated { get; set; }
    public int SubProjectsCreated { get; set; }
    public int SubProjectsApproved { get; set; }
    public int SubProjectsCreatedAndApproved { get; set; }
    public int SubProjectsAlreadyLinked { get; set; }
    public List<ImportRowFailureDto> Failed { get; set; } = new();
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public string PlanStatus { get; set; } = string.Empty;
}
