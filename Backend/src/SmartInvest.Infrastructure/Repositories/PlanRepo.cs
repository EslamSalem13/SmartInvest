namespace SmartInvest.Infrastructure.Repositories
{
    public class PlanRepo :  GenericRepository<Plan>, IPlanRepo    
    {
       
            public PlanRepo(AppDbContext Context) : base(Context) { }

            // Plan by Id with Projects
            public Plan? GetPlanWithProjectsById(int planId)
            {
                return Context.Plans
                    .Include(p => p.PlanProjects)
                    .FirstOrDefault(p => p.PlanId == planId);
            }
            // currently active Plan
            public Plan? GetCurrentPlan()
            {
                return Context.Plans
                    .Where(p => !p.IsClosed)
                    .OrderByDescending(p => p.StartDate)
                    .Include(p => p.PlanProjects)
                    .FirstOrDefault();
            }
            // filter by Plan is approved or suggested
            public Plan GetPlanByStatus(PlanStatus Status, string PlanName)
            {
                return
                    Context.Plans
                    .Where(a => a.PlanStatus == Status && a.PlanName == PlanName)
                    .Include(p => p.PlanProjects)
                    .FirstOrDefault()!;
            }
        }
    
}
