using SmartInvest.Domain.Entities;

namespace SmartInvest.Domain.Interfaces;

public interface IMeasurementRepository : IGenericRepository<Measurement>
{
    Task<IReadOnlyList<Measurement>> GetAllWithSubProgramsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Measurement>> GetApplicableForSubProgramAsync(int subProgramId, CancellationToken cancellationToken = default);

    Task<Measurement?> GetByIdWithSubProgramsAsync(int id, CancellationToken cancellationToken = default);
}
