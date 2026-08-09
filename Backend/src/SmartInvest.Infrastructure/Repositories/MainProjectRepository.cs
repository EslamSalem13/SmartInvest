using Microsoft.EntityFrameworkCore;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Repositories;

public class MainProjectRepository : GenericRepository<MainProject>, IMainProjectRepository
{
    public MainProjectRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<MainProject> Items, int TotalCount)> GetAllWithDetailsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        // ملاحظة: عمدًا بدون Include(SubProjects) هنا — تحميل كل المشروعات الفرعية بكل أعمدتها
        // فقط عشان نحسب Count/Sum كان بيعمل join ضخم (25 ثانية مع ~2000 مشروع فرعي).
        // الأرقام المجمّعة بتتحسب منفصل في GetSubProjectAggregatesAsync عبر GROUP BY.
        var query = DbSet
            .Include(x => x.SubProgram).ThenInclude(sp => sp.MainProgram)
            .AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.MainProjectId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<Dictionary<int, (int Count, decimal BankSum, decimal SelfSum)>> GetSubProjectAggregatesAsync(
        IReadOnlyCollection<int> mainProjectIds, CancellationToken cancellationToken = default)
    {
        var rows = await Context.Set<SubProject>()
            .Where(sp => mainProjectIds.Contains(sp.MainProjectId))
            .GroupBy(sp => sp.MainProjectId)
            .Select(g => new
            {
                MainProjectId = g.Key,
                Count = g.Count(),
                BankSum = g.Sum(x => x.BankFunding),
                SelfSum = g.Sum(x => x.SelfFunding),
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(r => r.MainProjectId, r => (r.Count, r.BankSum, r.SelfSum));
    }

    public async Task<MainProject?> GetWithSubProjectsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.SubProgram).ThenInclude(sp => sp.MainProgram)
            .Include(x => x.SubProjects).ThenInclude(sp => sp.Priority)
            .Include(x => x.SubProjects).ThenInclude(sp => sp.Status)
            .Include(x => x.SubProjects).ThenInclude(sp => sp.ProjectLevel)
            .Include(x => x.SubProjects).ThenInclude(sp => sp.ComponentType)
            .FirstOrDefaultAsync(x => x.MainProjectId == id, cancellationToken);
    }

    public async Task<IReadOnlyList<MainProject>> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        return await DbSet
            .Where(x => x.MainProjectName == trimmed)
            .ToListAsync(cancellationToken);
    }
}