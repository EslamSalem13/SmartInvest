namespace SmartInvest.Domain.Interfaces
{
    public interface IPlanRepo : IGenericRepository<Plan>
    {
        Plan? GetPlanWithProjectsById(int planId);
        Plan? GetCurrentPlan();
        List<Plan>? GetPlanByStatusAndName(PlanStatus? Status, string? PlanName);
        Task AddExistingProject(int PlanId, int ProjectId);
        Task AddProject(int PlanId, SubProject project);
        void DeleteProjectFromPlan(int PlanId, int ProjectId);
    }
}
