namespace SmartInvest.Infrastructure.Repositories
{
    public class PlanRepo :  GenericRepository<Plan>, IPlanRepo    
    {
       
            public PlanRepo(AppDbContext Context) : base(Context) { }

            // Plan by Id with Projects
            public Plan? GetPlanWithProjectsById(int planId)
            {
                return Context.Plans
                    .Include(p => p.PlanProjects!)
                    .ThenInclude(pp => pp.SubProject!)
                    .Include(p => p.FinancialYear)
                    .FirstOrDefault(p => p.PlanId == planId);
            }
        // currently active Plan
            public Plan? GetCurrentPlan()
        {
            return Context.Plans
                .Where(p => !p.IsClosed)
                .OrderByDescending(p => p.StartDate)
                .Include(p => p.PlanProjects!)
                  .ThenInclude(pp => pp.SubProject!)
                .Include(p => p.FinancialYear)
                .FirstOrDefault();
        }

        // filter by Plan is approved or suggested
            public List<Plan>? GetPlanByStatusAndName(PlanStatus? Status, string? PlanName)
            {
                 var  Query = Context.Plans
                    .Include(p => p.FinancialYear)
                    .AsQueryable();
           
                if(Status != null && !string.IsNullOrEmpty(PlanName))
                {
                    Query = Query.Where(p => p.PlanStatus == Status && p.PlanName == PlanName);
                }
                else if (Status != null)
                {
                    Query = Query.Where(p => p.PlanStatus == Status);
                }
                else if (!string.IsNullOrEmpty(PlanName))
                {
                    Query = Query.Where(p => p.PlanName == PlanName);
                }
             return Query.ToList();
          }     
            public async Task AddExistingProject(int PlanId, int ProjectId)
            {
                var project = await Context.SubProjects.FindAsync(ProjectId);
                if (project != null)
                {
                    await Context.PlanProjects.AddAsync(new PlanProject { PlanId = PlanId, SubProjectId = ProjectId });
                }
            }
            
            public async Task AddProject(int PlanId,SubProject project)
            {
                await Context.SubProjects.AddAsync(project);
                await Context.PlanProjects.AddAsync(new PlanProject { PlanId = PlanId, SubProject = project });
            }
            
            public void DeleteProjectFromPlan(int PlanId, int ProjectId)
        {
            var planProject = Context.PlanProjects.FirstOrDefault(p => p.PlanId == PlanId && p.SubProjectId == ProjectId);
            if (planProject != null)
            {
                Context.PlanProjects.Remove(planProject);
            }
        }
    }
}
