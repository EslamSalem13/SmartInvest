using SmartInvest.Domain.Entities;

namespace SmartInvest.Domain.Interfaces;

public interface IMainProjectRepository : IGenericRepository<MainProject>
{
    Task<IReadOnlyList<MainProject>> GetAllWithDetailsAsync(CancellationToken cancellationToken = default);

    Task<MainProject?> GetWithSubProjectsAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MainProject>> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}