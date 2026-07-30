using Microsoft.EntityFrameworkCore;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Interfaces;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Repositories;

public class MeasurementRepository : GenericRepository<Measurement>, IMeasurementRepository
{
    public MeasurementRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Measurement>> GetAllWithSubProgramsAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.MeasurementSubPrograms).ThenInclude(l => l.SubProgram)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Measurement>> GetApplicableForSubProgramAsync(int subProgramId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.MeasurementSubPrograms).ThenInclude(l => l.SubProgram)
            .Where(x => x.MeasurementSubPrograms.Any(l => l.SubProgramId == subProgramId))
            .ToListAsync(cancellationToken);
    }

    public async Task<Measurement?> GetByIdWithSubProgramsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Include(x => x.MeasurementSubPrograms).ThenInclude(l => l.SubProgram)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }
}
