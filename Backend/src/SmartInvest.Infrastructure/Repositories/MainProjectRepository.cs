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

    public async Task<IReadOnlyList<MainProject>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.SubProgram).ThenInclude(sp => sp.MainProgram)
            .Include(x => x.SubProjects)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
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