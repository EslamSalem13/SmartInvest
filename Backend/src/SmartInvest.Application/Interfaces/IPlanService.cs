namespace SmartInvest.Application.Interfaces;

public interface IPlanService
{
    List<Plan>? GetPlansByNameAndStatus(PlanStatus? planStatus, string? planName);
    Plan GetPlanDetails(int planId);
    Plan GetCurrentPlan();
    Task AddPlan(Plan plan);
    Task UpdatePlan(Plan plan);
    Task DeletePlan(Plan plan);
    Task DeletePlanById(int planId);
    Task AddProjectToPlan(int Planid, SubProject project);
    Task AddExistingProjectToPlan(int Planid, int ProjectId);
    Task DeleteProjectFromPlan(int PlanId, int ProjectId);

    Task<Plan> ApproveAsync(int planId, DateTime approvalDate);
}
