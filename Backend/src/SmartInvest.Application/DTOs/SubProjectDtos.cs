namespace SmartInvest.Application.DTOs;

public class ApproveSubProjectDto
{
    public string Code { get; set; } = string.Empty;
}

public class SubProjectListItemDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;

    public int MainProjectId { get; set; }
    public string MainProjectCode { get; set; } = string.Empty;
    public string MainProjectName { get; set; } = string.Empty;

    public int ProjectLevelId { get; set; }
    public string ProjectLevelName { get; set; } = string.Empty;
    public int ComponentTypeId { get; set; }
    public string ComponentTypeName { get; set; } = string.Empty;

    public int MarkazId { get; set; }
    public string MarkazName { get; set; } = string.Empty;

    public int PriorityId { get; set; }
    public string PriorityName { get; set; } = string.Empty;

    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public int? ExecutiveAgencyId { get; set; }
    public string? ExecutiveAgencyName { get; set; }
    public string? ContractorName { get; set; }

    public bool IsApproved { get; set; }
    public string? ApprovalCancellationReason { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ApprovalCancelledAt { get; set; }

    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }
    public decimal TotalCost { get; set; }
}

public class SubProjectDetailDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;

    public int MainProjectId { get; set; }
    public string MainProjectCode { get; set; } = string.Empty;
    public string MainProjectName { get; set; } = string.Empty;
    public int SubProgramId { get; set; }

    public int ProjectLevelId { get; set; }
    public string ProjectLevelName { get; set; } = string.Empty;
    public int ComponentTypeId { get; set; }
    public string ComponentTypeName { get; set; } = string.Empty;
    public int AccountingUnitId { get; set; }
    public string AccountingUnitName { get; set; } = string.Empty;
    public string ProjectNature { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Goal { get; set; }
    public string? SocialImpact { get; set; }
    public string? EconomicImpact { get; set; }
    public string? EnvironmentalImpact { get; set; }
    public string? GreenInvestmentLink { get; set; }

    public int MarkazId { get; set; }
    public string MarkazName { get; set; } = string.Empty;

    public int GovernorateId { get; set; }
    public string GovernorateName { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public int PriorityId { get; set; }
    public string PriorityName { get; set; } = string.Empty;

    public int StatusId { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public int? ExecutiveAgencyId { get; set; }
    public string? ExecutiveAgencyName { get; set; }

    public bool IsApproved { get; set; }
    public string? ApprovalCancellationReason { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ApprovalCancelledAt { get; set; }

    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }
    public decimal TotalCost { get; set; }

    public string? ContractorName { get; set; }
    public string? ContractTypeName { get; set; }
    public string? ContractNumber { get; set; }
    public decimal? ContractValue { get; set; }
    public IReadOnlyList<SubProjectFinancialYearDto> FinancialYears { get; set; }
        = new List<SubProjectFinancialYearDto>();

    public IReadOnlyList<ProjectSpecificationDto> Specifications { get; set; }
        = new List<ProjectSpecificationDto>();
}

public class CreateSubProjectDto
{
    public int MainProjectId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProjectLevelId { get; set; }
    public int ComponentTypeId { get; set; }
    public int AccountingUnitId { get; set; }
    public string ProjectNature { get; set; } = string.Empty;
    public int MarkazId { get; set; }
    public int PriorityId { get; set; }
    public int StatusId { get; set; }
    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public string? Goal { get; set; }
    public string? SocialImpact { get; set; }
    public string? EconomicImpact { get; set; }
    public string? EnvironmentalImpact { get; set; }
    public string? GreenInvestmentLink { get; set; }
}

public class UpdateSubProjectDto
{
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ProjectLevelId { get; set; }
    public int ComponentTypeId { get; set; }
    public int AccountingUnitId { get; set; }
    public string ProjectNature { get; set; } = string.Empty;
    public int MarkazId { get; set; }
    public int PriorityId { get; set; }
    public int StatusId { get; set; }
    public decimal BankFunding { get; set; }
    public decimal SelfFunding { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Description { get; set; }
    public string? Goal { get; set; }
    public string? SocialImpact { get; set; }
    public string? EconomicImpact { get; set; }
    public string? EnvironmentalImpact { get; set; }
    public string? GreenInvestmentLink { get; set; }
}

public class AssignExecutiveAgencyDto
{
    public int ExecutiveAgencyId { get; set; }
}
