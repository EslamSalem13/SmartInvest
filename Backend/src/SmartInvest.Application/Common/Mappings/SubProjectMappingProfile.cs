using AutoMapper;
using SmartInvest.Application.DTOs;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Application.Common.Mappings;

public class SubProjectMappingProfile : Profile
{
    public SubProjectMappingProfile()
    {
        CreateMap<SubProject, SubProjectListItemDto>()
            .ForMember(
                dest => dest.Id,
                opt => opt.MapFrom(src => src.SubProjectId))
            .ForMember(
                dest => dest.Code,
                opt => opt.MapFrom(src => src.SubProjectCode))
            .ForMember(
                dest => dest.Name,
                opt => opt.MapFrom(src => src.SubProjectName))
            .ForMember(
                dest => dest.MainProjectId,
                opt => opt.MapFrom(src => src.MainProjectId))
            .ForMember(
                dest => dest.MainProjectCode,
                opt => opt.MapFrom(src => src.MainProject.MainProjectCode))
            .ForMember(
                dest => dest.MainProjectName,
                opt => opt.MapFrom(src => src.MainProject.MainProjectName))
            .ForMember(
                dest => dest.MarkazId,
                opt => opt.MapFrom(src => src.MarkazId))
            .ForMember(
                dest => dest.MarkazName,
                opt => opt.MapFrom(src => src.Markaz.MarkazName))
            .ForMember(
                dest => dest.PriorityId,
                opt => opt.MapFrom(src => src.PriorityId))
            .ForMember(
                dest => dest.PriorityName,
                opt => opt.MapFrom(src => src.Priority.Priority))
            .ForMember(
                dest => dest.StatusId,
                opt => opt.MapFrom(src => src.StatusId))
            .ForMember(
                dest => dest.StatusName,
                opt => opt.MapFrom(src => src.Status.StatusName))
            .ForMember(
                dest => dest.ExecutiveAgencyId,
                opt => opt.MapFrom(src => src.ExecutiveAgencyId))
            .ForMember(
                dest => dest.ExecutiveAgencyName,
                opt => opt.MapFrom(src => src.ExecutiveAgency != null ? src.ExecutiveAgency.AgencyName : null))
            .ForMember(
                dest => dest.ContractorName,
                opt => opt.MapFrom(src => GetLatestContractorName(src)))
            .ForMember(
                dest => dest.ProjectLevelId,
                opt => opt.MapFrom(src => src.ProjectLevelId))
            .ForMember(
                dest => dest.ProjectLevelName,
                opt => opt.MapFrom(src => src.ProjectLevel.Name))
            .ForMember(
                dest => dest.ComponentTypeId,
                opt => opt.MapFrom(src => src.ComponentTypeId))
            .ForMember(
                dest => dest.ComponentTypeName,
                opt => opt.MapFrom(src => src.ComponentType.Name));

        CreateMap<SubProject, SubProjectDetailDto>()
            .ForMember(
                dest => dest.Id,
                opt => opt.MapFrom(src => src.SubProjectId))
            .ForMember(
                dest => dest.Code,
                opt => opt.MapFrom(src => src.SubProjectCode))
            .ForMember(
                dest => dest.Name,
                opt => opt.MapFrom(src => src.SubProjectName))
            .ForMember(
                dest => dest.Description,
                opt => opt.MapFrom(src => src.ProjectDescription))
            .ForMember(
                dest => dest.Goal,
                opt => opt.MapFrom(src => src.ProjectGoal))
            .ForMember(
                dest => dest.MainProjectId,
                opt => opt.MapFrom(src => src.MainProjectId))
            .ForMember(
                dest => dest.MainProjectCode,
                opt => opt.MapFrom(src => src.MainProject.MainProjectCode))
            .ForMember(
                dest => dest.MainProjectName,
                opt => opt.MapFrom(src => src.MainProject.MainProjectName))
            .ForMember(
                dest => dest.SubProgramId,
                opt => opt.MapFrom(src => src.MainProject.SubProgramId))
            .ForMember(
                dest => dest.SubProgramName,
                opt => opt.MapFrom(src => src.MainProject.SubProgram.SubProgramName))
            .ForMember(
                dest => dest.MainProgramName,
                opt => opt.MapFrom(src => src.MainProject.SubProgram.MainProgram.ProgramName))
            .ForMember(
                dest => dest.ContractorName,
                opt => opt.MapFrom(src => GetLatestAssignment(src).ContractorName))
            .ForMember(
                dest => dest.ContractTypeName,
                opt => opt.MapFrom(src => GetLatestAssignment(src).ContractTypeName))
            .ForMember(
                dest => dest.ContractDate,
                opt => opt.MapFrom(src => GetLatestAssignment(src).ContractDate))
            .ForMember(
                dest => dest.ContractValue,
                opt => opt.MapFrom(src => GetLatestAssignment(src).ContractValue))
            .ForMember(
                dest => dest.FinancialYears,
                opt => opt.MapFrom(src => src.FinancialYears.Select(fy => new SubProjectFinancialYearDto
                {
                    Id = fy.SubProjectFinancialYearId,
                    FinancialYearId = fy.FinancialYearId,
                    FinancialYearName = fy.FinancialYear.Name,
                    StartDate = fy.FinancialYear.StartDate,
                    EndDate = fy.FinancialYear.EndDate,
                    IsClosed = fy.FinancialYear.IsClosed,
                })))
            .ForMember(
                dest => dest.MarkazId,
                opt => opt.MapFrom(src => src.MarkazId))
            .ForMember(
                dest => dest.MarkazName,
                opt => opt.MapFrom(src => src.Markaz.MarkazName))
            .ForMember(
                dest => dest.GovernorateId,
                opt => opt.MapFrom(src => src.Markaz.GovernorateId))
            .ForMember(
                dest => dest.GovernorateName,
                opt => opt.MapFrom(src => src.Markaz.Governorate.GovernorateName))
            .ForMember(
                dest => dest.PriorityId,
                opt => opt.MapFrom(src => src.PriorityId))
            .ForMember(
                dest => dest.PriorityName,
                opt => opt.MapFrom(src => src.Priority.Priority))
            .ForMember(
                dest => dest.StatusId,
                opt => opt.MapFrom(src => src.StatusId))
            .ForMember(
                dest => dest.StatusName,
                opt => opt.MapFrom(src => src.Status.StatusName))
            .ForMember(
                dest => dest.ExecutiveAgencyId,
                opt => opt.MapFrom(src => src.ExecutiveAgencyId))
            .ForMember(
                dest => dest.ExecutiveAgencyName,
                opt => opt.MapFrom(src => src.ExecutiveAgency != null ? src.ExecutiveAgency.AgencyName : null))
            .ForMember(
                dest => dest.Specifications,
                opt => opt.MapFrom(src => src.ProjectSpecifications))
            .ForMember(
                dest => dest.ProjectLevelId,
                opt => opt.MapFrom(src => src.ProjectLevelId))
            .ForMember(
                dest => dest.ProjectLevelName,
                opt => opt.MapFrom(src => src.ProjectLevel.Name))
            .ForMember(
                dest => dest.ComponentTypeId,
                opt => opt.MapFrom(src => src.ComponentTypeId))
            .ForMember(
                dest => dest.ComponentTypeName,
                opt => opt.MapFrom(src => src.ComponentType.Name))
            .ForMember(
                dest => dest.AccountingUnitId,
                opt => opt.MapFrom(src => src.AccountingUnitId))
            .ForMember(
                dest => dest.AccountingUnitName,
                opt => opt.MapFrom(src => src.AccountingUnit.Name));

        CreateMap<CreateSubProjectDto, SubProject>()
            .ForMember(
                dest => dest.SubProjectName,
                opt => opt.MapFrom(src => src.Name))
            .ForMember(
                dest => dest.ProjectDescription,
                opt => opt.MapFrom(src => src.Description))
            .ForMember(
                dest => dest.ProjectGoal,
                opt => opt.MapFrom(src => src.Goal));

        CreateMap<UpdateSubProjectDto, SubProject>()
            .ForMember(
                dest => dest.SubProjectName,
                opt => opt.MapFrom(src => src.Name))
            .ForMember(
                dest => dest.ProjectDescription,
                opt => opt.MapFrom(src => src.Description))
            .ForMember(
                dest => dest.ProjectGoal,
                opt => opt.MapFrom(src => src.Goal));
    }

    private static string? GetLatestContractorName(SubProject subProject)
    {
        if (subProject.ProjectAssignments == null || !subProject.ProjectAssignments.Any())
        {
            return null;
        }

        var latestAssignment = subProject.ProjectAssignments
            .OrderByDescending(a => a.AssignmentDate)
            .First();

        return latestAssignment.Contractor?.ContractorName;
    }

    private static LatestAssignmentInfo GetLatestAssignment(SubProject subProject)
    {
        var assignment = subProject.ProjectAssignments?
            .OrderByDescending(a => a.AssignmentDate)
            .FirstOrDefault();

        return new LatestAssignmentInfo
        {
            ContractorName = assignment?.Contractor?.ContractorName,
            ContractTypeName = assignment?.ContractType?.ContractName,
            ContractDate = assignment?.ContractDate,
            ContractValue = assignment?.ContractValue,
        };
    }

    private class LatestAssignmentInfo
    {
        public string? ContractorName { get; set; }
        public string? ContractTypeName { get; set; }
        public DateTime? ContractDate { get; set; }
        public decimal? ContractValue { get; set; }
    }
}
