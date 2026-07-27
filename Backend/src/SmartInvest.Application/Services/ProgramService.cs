using SmartInvest.Application.DTOs.Program;

namespace SmartInvest.Application.Services
{
    public  class ProgramService :IProgramService
    {
        private readonly IProgramRepo programRepo;
        private readonly IUnitOfWork unitOfWork;
        private readonly IMapper mapper;

        public ProgramService(IProgramRepo programRepo, IUnitOfWork unitOfWork,IMapper mapper)
        {
            this.programRepo = programRepo;
            this.unitOfWork = unitOfWork;
            this.mapper = mapper;
        }

        public async Task<IEnumerable<MainProgram>> GetProgramsTreeAsync(string? planName, PlanStatus? planStatus, string? mainProgramName)
        {
            return await programRepo.GetProgramsTreeAsync(planName, planStatus, mainProgramName);
        }
        public async Task<IEnumerable<MainProgramDto>> GetCurrentProgramsTreeAsync()
        {
            var currentProgramsEntities = await programRepo.GetCurrentProgramsTreeAsync();

            var programsDto = mapper.Map<IEnumerable<MainProgramDto>>(currentProgramsEntities);

            var filteredTree = programsDto
                .Where(mp => mp.SubPrograms!.Sum(sp => sp.ProjectsCount) > 0)
                .ToList();

            return filteredTree;
        }
        
    }
}
