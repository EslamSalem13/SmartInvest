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
            .ReverseMap();

            CreateMap<Plan, AddAndEditPlanInfoDto>()
            .ReverseMap();

            CreateMap<SubProject, AddNewProjectDto >()
           .ReverseMap();
            CreateMap<Plan, PlanWithoutProjectsDto>()
           .ReverseMap();

             CreateMap<SubProject, ProjectInfoDto>()
            .ForMember(des => des.ExecutiveAgencyName, opt => opt.MapFrom(src => src.ExecutiveAgency!.AgencyName))
            .ReverseMap();
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
