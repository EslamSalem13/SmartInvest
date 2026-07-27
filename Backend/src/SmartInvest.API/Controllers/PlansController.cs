namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/plans")]
[Authorize]
public class PlansController : ControllerBase
{
    private readonly IPlanService planService;
    private readonly IMapper mapper;

    public PlansController(IPlanService planService, IMapper mapper)
    {
        this.planService = planService;
        this.mapper = mapper;
    }

    [HttpGet]
    public IActionResult GetPlansByNameAndStatus(PlanStatus? planStatus, string? planName)
    {
        List<Plan>? PlansFromDb = planService.GetPlansByNameAndStatus(planStatus, planName);

        var plans = mapper.Map<List<PlanWithoutProjectsDto>>(PlansFromDb);
        return Ok(plans);
    }

    [HttpGet("{id}")]
    public IActionResult GetPlanDetailsById(int id)
    {
        Plan PlanFromDb = planService.GetPlanDetails(id);

        if (PlanFromDb == null)
        {
            return NotFound();
        }

        var Plan = mapper.Map<PlanInfoDto>(PlanFromDb);
        return Ok(Plan);
    }

    [HttpGet("Current")]
    public IActionResult GetCurrentPlan()
    {
        Plan PlanFromDb = planService.GetCurrentPlan();

        if (PlanFromDb == null)
        {
            return NotFound();
        }

        var plan = mapper.Map<PlanInfoDto>(PlanFromDb);
        return Ok(plan);
    }

    [HttpPost]
    public async Task<IActionResult> AddPlan(AddAndEditPlanInfoDto Plandto)
    {
        var plan = mapper.Map<Plan>(Plandto);
        await planService.AddPlan(plan);

        return CreatedAtAction(nameof(GetPlanDetailsById), new { id = plan.PlanId }, plan);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlan(int id, AddAndEditPlanInfoDto planDto)
    {
        var plan = planService.GetPlanDetails(id);

        if (plan == null)
        {
            return NotFound();
        }

        mapper.Map(planDto, plan);
        await planService.UpdatePlan(plan);

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlan(int id)
    {
        await planService.DeletePlanById(id);
        return NoContent();
    }

    //// manage Projects in A Plan /////

    [HttpPost("{planId}/newProject")]
    public async Task<IActionResult> AddNewProjectToPlan(int planId, AddNewProjectDto projectDto)
    {
        var project = mapper.Map<SubProject>(projectDto);
        await planService.AddProjectToPlan(planId, project);
        var projectToReturn = mapper.Map<ProjectInfoDto>(project);
        return Ok(projectToReturn);
    }

    [HttpPost("{Planid}/existingProject/{ProjectId}")]
    public async Task<IActionResult> AddExistingProjectToPlan(int Planid, int ProjectId)
    {
        await planService.AddExistingProjectToPlan(Planid, ProjectId);
        return NoContent();
    }

    [HttpDelete("{planId}/projects/{ProjectId}")]
    public async Task<IActionResult> DeleteProjectFromPlan(int planId, int ProjectId)
    {
        await planService.DeleteProjectFromPlan(planId, ProjectId);
        return NoContent();
    }
}
