namespace SmartInvest.Application.Services;

public class PlanService : IPlanService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IPlanRepo planRepo;

    public PlanService(IUnitOfWork unitOfWork, IPlanRepo planRepo)
    {
        this.unitOfWork = unitOfWork;
        this.planRepo = planRepo;
    }
    // Plans with filter
    public Plan GetPlansByNameAndStatus(PlanStatus planStatus, string planName)
    {
        return planRepo.GetPlanByStatus(planStatus, planName);
    }
    public Task<Plan> GetPlanDetails(int planId)
    {
        return planRepo.GetByIdAsync(planId)!;
    }
    public Plan GetCurrentPlan()
    {
        return planRepo.GetCurrentPlan()!;
    }
    public async Task AddPlan(Plan plan)
    {
        await planRepo.AddAsync(plan);
        await unitOfWork.SaveChangesAsync();
    }
    public async Task UpdatePlan(Plan plan)
    {
        planRepo.Update(plan);
        await unitOfWork.SaveChangesAsync();
    }
    public async Task DeletePlan(Plan plan)
    {
        planRepo.Remove(plan);
        await unitOfWork.SaveChangesAsync();
    }
    ////////////// manage PlanProject  ///////////////




}
