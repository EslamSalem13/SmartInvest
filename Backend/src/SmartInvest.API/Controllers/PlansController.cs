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

    
}
