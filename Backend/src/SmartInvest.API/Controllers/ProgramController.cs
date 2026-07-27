using SmartInvest.Application.DTOs.Program;

namespace SmartInvest.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProgramController : ControllerBase
    {
        private readonly IMapper mapper;
        private readonly IProgramService programService;

        public ProgramController(IMapper mapper, IProgramService programService)
        {
            this.mapper = mapper;
            this.programService = programService;
        }
        [HttpGet]
        public async Task<IActionResult> GetFilterdPrograms([FromQuery] string? planName, [FromQuery] PlanStatus? planStatus, [FromQuery] string? mainProgramName)
        {
            var programsEntities = await programService.GetProgramsTreeAsync(planName, planStatus, mainProgramName);

            var programsDto = mapper.Map<IEnumerable<MainProgramDto>>(programsEntities);

            return Ok(programsDto);
        }
        [HttpGet("current-programs")]
        public async Task<IActionResult> GetCurrentProgramsTree()
        {
            var result = await programService.GetCurrentProgramsTreeAsync();

            return Ok(result);
        }
    }
}
