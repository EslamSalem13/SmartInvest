using SmartInvest.Application.DTOs.Program;

namespace SmartInvest.Application.Common.Mappings
{
    public class PlansAndPrograms : Profile
    {
        public PlansAndPrograms()
        {
            #region Plans

            CreateMap<Plan, PlanInfoDto>()
            .ForMember(des => des.Projects, opt => opt.MapFrom(src => src.PlanProjects!.Select(p=>p.SubProject)))
            .ForMember(d => d.FinancialYearName, o => o.MapFrom(s => s.FinancialYear!.Name))
            .ReverseMap();

            CreateMap<Plan, AddAndEditPlanInfoDto>()
            .ReverseMap();

            CreateMap<SubProject, AddNewProjectDto>()
           .ForMember(
               dest => dest.ProjectLevelName,
               opt => opt.MapFrom(src => src.ProjectLevel.Name))
           .ForMember(
               dest => dest.ComponentTypeName,
               opt => opt.MapFrom(src => src.ComponentType.Name))
           .ForMember(
               dest => dest.AccountingUnitName,
               opt => opt.MapFrom(src => src.AccountingUnit.Name));
            CreateMap<Plan, PlanWithoutProjectsDto>()
           .ForMember(d => d.FinancialYearName, o => o.MapFrom(s => s.FinancialYear!.Name))
           .ReverseMap();

             CreateMap<SubProject, ProjectInfoDto>()
            .ForMember(des => des.ExecutiveAgencyName, opt => opt.MapFrom(src => src.ExecutiveAgency != null ? src.ExecutiveAgency.AgencyName : null))
            .ForMember(des => des.ProjectLevel, opt => opt.MapFrom(src => src.ProjectLevel != null ? src.ProjectLevel.Name : string.Empty));
            #endregion

            #region   MainPrograms & SubPrograms

            CreateMap<MainProgram, MainProgramDto>();

            CreateMap<SubProgram, SubProgramDto>()
                .ForMember(dest => dest.ProjectsCount, opt => opt.MapFrom(src =>
                    src.MainProjects
                       .SelectMany(mp => mp.SubProjects)
                       .Count(sp => sp.PlanProjects.Any())
                ));

            #endregion

        }
    }
}
