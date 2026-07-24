using SmartInvest.Application.DTOs;

namespace SmartInvest.API.Controllers;

[ApiController]
[Route("api/plans")]
[Authorize]
public class PlansController : ControllerBase
{
    private readonly IPlanService planService;

    public PlansController(IPlanService planService)
    {
        this.planService = planService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await planService.GetAllAsync(cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PlanDetailDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await planService.GetByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<PlanDto>> Create(CreatePlanDto dto, CancellationToken cancellationToken)
    {
        var result = await planService.CreateAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<PlanDto>> Update(int id, UpdatePlanDto dto, CancellationToken cancellationToken)
    {
        var result = await planService.UpdateAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await planService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/suggested-projects")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<ActionResult<PlanDetailDto>> AddSuggestedProject(int id, AddSuggestedProjectDto dto, CancellationToken cancellationToken)
    {
        var result = await planService.AddSuggestedProjectAsync(id, dto.SubProjectId, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}/suggested-projects/{subProjectId:int}")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> RemoveSuggestedProject(int id, int subProjectId, CancellationToken cancellationToken)
    {
        await planService.RemoveSuggestedProjectAsync(id, subProjectId, cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:int}/approve")]
    [Authorize(Roles = Roles.PlanningManager)]
    public async Task<ActionResult<PlanDto>> Approve(int id, ApprovePlanDto dto, CancellationToken cancellationToken)
    {
        var result = await planService.ApproveAsync(id, dto.ApprovalDate, cancellationToken);
        return Ok(result);
    }
}
