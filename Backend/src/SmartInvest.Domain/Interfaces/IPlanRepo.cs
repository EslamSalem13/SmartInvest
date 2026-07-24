namespace SmartInvest.Domain.Interfaces
{
    public interface IPlanRepo : IGenericRepository<Plan>
    {
        Plan? GetPlanWithProjectsById(int planId);
        Plan? GetCurrentPlan();
        Plan GetPlanByStatus(PlanStatus Status, string PlanName);
    }
}
