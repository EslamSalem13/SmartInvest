using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardOverviewDto> GetOverviewAsync(int? financialYearId, CancellationToken cancellationToken = default);
}
