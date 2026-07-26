namespace SmartInvest.Infrastructure.Repositories
{
    public class ProgramRepo : GenericRepository<MainProgram>, IProgramRepo
    {
        public ProgramRepo(AppDbContext Context) : base(Context) { }

        // filters
        public async Task<IEnumerable<MainProgram>> GetProgramsTreeAsync(string? planName, PlanStatus? planStatus, string? mainProgramName)
        {
            var query = Context.MainPrograms.AsQueryable();

            if (!string.IsNullOrWhiteSpace(mainProgramName))
            {
                query = query.Where(mp => mp.ProgramName.Contains(mainProgramName));
            }

            query = query
                .Include(mp => mp.SubPrograms!)
                    .ThenInclude(sp => sp.MainProjects)
                        .ThenInclude(mainProj => mainProj.SubProjects)
                            .ThenInclude(subProj => subProj.PlanProjects.Where(pp =>
                                (string.IsNullOrEmpty(planName) || pp.Plan.PlanName == planName) &&
                                (planStatus == null || pp.Plan.PlanStatus == planStatus)
                            ))
                            .ThenInclude(pp => pp.Plan);

            return await query.ToListAsync();
        }

        // current Program as default
        public async Task<IEnumerable<MainProgram>> GetCurrentProgramsTreeAsync()
        {
            var query = Context.MainPrograms
                .Include(mp => mp.SubPrograms!)
                    .ThenInclude(sp => sp.MainProjects)
                        .ThenInclude(mainProj => mainProj.SubProjects)
                            .ThenInclude(subProj => subProj.PlanProjects.Where(pp =>
                                pp.Plan.PlanStatus == PlanStatus.Approved &&
                                pp.Plan.IsClosed == false
                            ))
                            .ThenInclude(pp => pp.Plan);

            return await query.ToListAsync();
        }

        // {will be added if bussiness need them}
        // post Mainprogram

        // post SubProgram 

        // put MainProgram

        //put SubProgram

    }
}
