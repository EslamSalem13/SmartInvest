namespace SmartInvest.Application.Common.Mappings;

public class MainProjectMappingProfile : Profile
{
    public MainProjectMappingProfile()
    {
        CreateMap<MainProject, MainProjectListItemDto>()
            .ForMember(
                dest => dest.Id,
                opt => opt.MapFrom(src => src.MainProjectId))
            .ForMember(
                dest => dest.Code,
                opt => opt.MapFrom(src => src.MainProjectCode))
            .ForMember(
                dest => dest.IsApproved,
                opt => opt.MapFrom(src => src.IsApproved))
            .ForMember(
                dest => dest.Name,
                opt => opt.MapFrom(src => src.MainProjectName))
            .ForMember(
                dest => dest.SubProgramId,
                opt => opt.MapFrom(src => src.SubProgramId))
            .ForMember(
                dest => dest.SubProgramName,
                opt => opt.MapFrom(src => src.SubProgram.SubProgramName))
            .ForMember(
                dest => dest.MainProgramName,
                opt => opt.MapFrom(src => src.SubProgram.MainProgram.ProgramName))
            // SubProjectsCount/TotalBankFunding/TotalSelfFunding عمدًا مش متعرّفين هنا —
            // src.SubProjects مش محمّل (شوف MainProjectRepository.GetAllWithDetailsAsync)،
            // القيم دي بتتحط يدويًا في MainProjectService.GetAllAsync من استعلام GROUP BY منفصل
            .ForMember(dest => dest.SubProjectsCount, opt => opt.Ignore())
            .ForMember(dest => dest.TotalBankFunding, opt => opt.Ignore())
            .ForMember(dest => dest.TotalSelfFunding, opt => opt.Ignore());

        CreateMap<MainProject, MainProjectDetailDto>()
            .ForMember(
                dest => dest.Id,
                opt => opt.MapFrom(src => src.MainProjectId))
            .ForMember(
                dest => dest.Code,
                opt => opt.MapFrom(src => src.MainProjectCode))
            .ForMember(
                dest => dest.IsApproved,
                opt => opt.MapFrom(src => src.IsApproved))
            .ForMember(
                dest => dest.Name,
                opt => opt.MapFrom(src => src.MainProjectName))
            .ForMember(
                dest => dest.SubProgramId,
                opt => opt.MapFrom(src => src.SubProgramId))
            .ForMember(
                dest => dest.SubProgramName,
                opt => opt.MapFrom(src => src.SubProgram.SubProgramName))
            .ForMember(
                dest => dest.MainProgramName,
                opt => opt.MapFrom(src => src.SubProgram.MainProgram.ProgramName))
            .ForMember(
                dest => dest.SubProjects,
                opt => opt.MapFrom(src => src.SubProjects));

        CreateMap<CreateMainProjectDto, MainProject>()
            .ForMember(
                dest => dest.MainProjectCode,
                opt => opt.MapFrom(src => src.Code))
            .ForMember(
                dest => dest.MainProjectName,
                opt => opt.MapFrom(src => src.Name));

        CreateMap<UpdateMainProjectDto, MainProject>()
            .ForMember(
                dest => dest.MainProjectCode,
                opt => opt.MapFrom(src => src.Code))
            .ForMember(
                dest => dest.MainProjectName,
                opt => opt.MapFrom(src => src.Name));
    }
}