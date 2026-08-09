using SmartInvest.Domain.Entities;

namespace SmartInvest.Domain.Interfaces;

public interface IMainProjectRepository : IGenericRepository<MainProject>
{
    Task<(IReadOnlyList<MainProject> Items, int TotalCount)> GetAllWithDetailsAsync(
        int page, int pageSize, CancellationToken cancellationToken = default);

    Task<Dictionary<int, (int Count, decimal BankSum, decimal SelfSum)>> GetSubProjectAggregatesAsync(
        IReadOnlyCollection<int> mainProjectIds, CancellationToken cancellationToken = default);

    Task<MainProject?> GetWithSubProjectsAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MainProject>> FindByNameAsync(string name, CancellationToken cancellationToken = default);
}