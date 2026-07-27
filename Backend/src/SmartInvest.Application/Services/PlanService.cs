using SmartInvest.Application.Common.Exceptions;

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
    public List<Plan>? GetPlansByNameAndStatus(PlanStatus? planStatus, string? planName)
    {
        return planRepo.GetPlanByStatusAndName(planStatus, planName);
    }
    public Plan GetPlanDetails(int planId)
    {
        return  planRepo.GetPlanWithProjectsById(planId)!;
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
    public async Task DeletePlanById(int planId)
    {
        var plan = await planRepo.GetByIdAsync(planId);
        if (plan != null)
        {
            planRepo.Remove(plan);
            await unitOfWork.SaveChangesAsync();
        }
    }
    ////////////// manage Projects in A Plan  ///////////////
    public async Task AddExistingProjectToPlan(int Planid, int ProjectId)
    {
        await planRepo.AddExistingProject(Planid, ProjectId);
        await unitOfWork.SaveChangesAsync();
    }
    public async Task AddProjectToPlan(int Planid,SubProject project)
    {
        await planRepo.AddProject(Planid, project);
        await unitOfWork.SaveChangesAsync();
    }
    public async Task DeleteProjectFromPlan(int PlanId, int ProjectId)
    {
        planRepo.DeleteProjectFromPlan(PlanId, ProjectId);
        await unitOfWork.SaveChangesAsync();
    }

    public async Task<Plan> ApproveAsync(int planId, DateTime approvalDate)
    {
        var plan = planRepo.GetPlanWithProjectsById(planId);
        if (plan == null)
        {
            throw new NotFoundException($"الخطة رقم {planId} غير موجودة");
        }

        if (plan.ApprovalDate.HasValue)
        {
            throw new BusinessRuleException("تم اعتماد هذه الخطة بالفعل");
        }

        plan.ApprovalDate = approvalDate;
        plan.PlanStatus = PlanStatus.Approved;

        planRepo.Update(plan);
        await unitOfWork.SaveChangesAsync();

        return plan;
    }
}
