using Microsoft.EntityFrameworkCore;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Repositories;

public class SubProjectRepository : GenericRepository<SubProject>, ISubProjectRepository
{
    public SubProjectRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<SubProject?> GetWithDetailsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.MainProject).ThenInclude(m => m.SubProgram).ThenInclude(sp => sp.MainProgram)
            .Include(x => x.Markaz).ThenInclude(m => m.Governorate)
            .Include(x => x.Priority)
            .Include(x => x.Status)
            .Include(x => x.ExecutiveAgency)
            .Include(x => x.ProjectSpecifications)
            .Include(x => x.ProjectLevel)
            .Include(x => x.ComponentType)
            .Include(x => x.AccountingUnit)
            .Include(x => x.ProjectAssignments).ThenInclude(a => a.Contractor)
            .Include(x => x.ProjectAssignments).ThenInclude(a => a.ContractType)
            .Include(x => x.FinancialYears).ThenInclude(fy => fy.FinancialYear)
            .FirstOrDefaultAsync(x => x.SubProjectId == id, cancellationToken);
    }

    public async Task<IReadOnlyList<SubProject>> GetByExecutiveAgencyAsync(int executiveAgencyId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.MainProject)
            .Where(x => x.ExecutiveAgencyId == executiveAgencyId)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<SubProject> Items, int TotalCount)> SearchAsync(
        int? mainProjectId,
        int? mainProgramId,
        int? subProgramId,
        int? markazId,
        int? priorityId,
        int? statusId,
        int? financialYearId,
        string? searchTerm,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet
            .Include(x => x.MainProject).ThenInclude(m => m.SubProgram).ThenInclude(sp => sp.MainProgram)
            .Include(x => x.Markaz)
            .Include(x => x.Priority)
            .Include(x => x.Status)
            .Include(x => x.ExecutiveAgency)
            .Include(x => x.ProjectAssignments).ThenInclude(a => a.Contractor)
            .Include(x => x.ProjectLevel)
            .Include(x => x.ComponentType)
            .AsQueryable();

        if (mainProjectId.HasValue)
        {
            query = query.Where(x => x.MainProjectId == mainProjectId);
        }

        if (subProgramId.HasValue)
        {
            query = query.Where(x => x.MainProject.SubProgramId == subProgramId);
        }

        if (mainProgramId.HasValue)
        {
            query = query.Where(x => x.MainProject.SubProgram.ProgramId == mainProgramId);
        }

        if (markazId.HasValue)
        {
            query = query.Where(x => x.MarkazId == markazId);
        }

        if (priorityId.HasValue)
        {
            query = query.Where(x => x.PriorityId == priorityId);
        }

        if (statusId.HasValue)
        {
            query = query.Where(x => x.StatusId == statusId);
        }

        if (financialYearId.HasValue)
        {
            query = query.Where(x => x.FinancialYears.Any(y => y.FinancialYearId == financialYearId));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(x =>
                x.SubProjectName.Contains(searchTerm) ||
                (x.SubProjectCode != null && x.SubProjectCode.Contains(searchTerm)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(x => x.MainProjectId).ThenBy(x => x.SubProjectId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(x =>
            x.SubProjectCode == code && (excludeId == null || x.SubProjectId != excludeId),
            cancellationToken);
    }

    public async Task<bool> NameExistsAsync(string name, int mainProjectId, int? excludeId = null, CancellationToken cancellationToken = default)
    {
        var trimmed = (name ?? string.Empty).Trim();
        // المقارنة تعتمد على collation قاعدة البيانات (SQL Server افتراضيًا case-insensitive)
        // النطاق يقتصر على المشروع الرئيسي نفسه، بما يطابق الفهرس المركب (MainProjectId, SubProjectName)
        return await DbSet.AnyAsync(x =>
            x.MainProjectId == mainProjectId && x.SubProjectName == trimmed && (excludeId == null || x.SubProjectId != excludeId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<SubProject>> FindByNameWithinMainProjectAsync(string name, int mainProjectId, CancellationToken cancellationToken = default)
    {
        var trimmed = name.Trim();
        return await DbSet
            .Where(x => x.SubProjectName == trimmed && x.MainProjectId == mainProjectId)
            .ToListAsync(cancellationToken);
    }
}
