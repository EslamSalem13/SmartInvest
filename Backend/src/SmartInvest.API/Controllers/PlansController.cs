using SmartInvest.Application.DTOs;

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
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> AddPlan(AddAndEditPlanInfoDto Plandto)
    {
        var plan = mapper.Map<Plan>(Plandto);
        await planService.AddPlan(plan);

        return CreatedAtAction(nameof(GetPlanDetailsById), new { id = plan.PlanId }, plan);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> UpdatePlan(int id, AddAndEditPlanInfoDto planDto)
    {
        var plan = planService.GetPlanDetails(id);

        if (plan == null)
        {
            return NotFound();
        }

        var status = plan.PlanStatus;
        var approvalDate = plan.ApprovalDate;
        mapper.Map(planDto, plan);
        plan.PlanStatus = status;
        plan.ApprovalDate = approvalDate;
        await planService.UpdatePlan(plan);

        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.ManagementStaff)]
    public async Task<IActionResult> DeletePlan(int id)
    {
        await planService.DeletePlanById(id);
        return NoContent();
    }

    //// manage Projects in A Plan /////

    [HttpPost("{planId}/newProject")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> AddNewProjectToPlan(int planId, AddNewProjectDto projectDto)
    {
        // AddNewProjectDto's ProjectLevel/ComponentType/AccountingUnit are now FK-based (Name فقط للقراءة)،
        // لذا لا يمكن الاعتماد على AutoMapper لبناء SubProject من الـ dto - يتم بناء الكيان يدويًا هنا.
        var project = new SubProject
        {
            SubProjectName = projectDto.SubProjectName,
            MainProjectId = projectDto.MainProjectId,
            ProjectLevelId = projectDto.ProjectLevelId,
            ComponentTypeId = projectDto.ComponentTypeId,
            AccountingUnitId = projectDto.AccountingUnitId,
            ProjectNature = projectDto.ProjectNature,
            GreenInvestmentLink = projectDto.GreenInvestmentLink,
            ProjectDescription = projectDto.ProjectDescription,
            ProjectGoal = projectDto.ProjectGoal,
            SocialImpact = projectDto.SocialImpact,
            EconomicImpact = projectDto.EconomicImpact,
            EnvironmentalImpact = projectDto.EnvironmentalImpact,
            MarkazId = projectDto.MarkazId,
            PriorityId = projectDto.PriorityId,
            ExecutiveAgencyId = projectDto.ExecutiveAgencyId,
            Latitude = projectDto.Latitude,
            Longitude = projectDto.Longitude,
            StatusId = projectDto.StatusId,
        };

        await planService.AddProjectToPlan(planId, project);
        var projectToReturn = mapper.Map<ProjectInfoDto>(project);
        return Ok(projectToReturn);
    }

    [HttpPost("{Planid}/existingProject/{ProjectId}")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> AddExistingProjectToPlan(int Planid, int ProjectId)
    {
        await planService.AddExistingProjectToPlan(Planid, ProjectId);
        return NoContent();
    }

    [HttpDelete("{planId}/projects/{ProjectId}")]
    [Authorize(Roles = Roles.PlanningStaff)]
    public async Task<IActionResult> DeleteProjectFromPlan(int planId, int ProjectId)
    {
        await planService.DeleteProjectFromPlan(planId, ProjectId);
        return NoContent();
    }

    [HttpPut("{id:int}/approve")]
    [Authorize(Roles = Roles.ManagementStaff)]
    public async Task<IActionResult> Approve(int id, ApprovePlanDto dto)
    {
        var plan = await planService.ApproveAsync(id, dto.ApprovalDate);
        var result = mapper.Map<PlanInfoDto>(plan);
        return Ok(result);
    }
}
