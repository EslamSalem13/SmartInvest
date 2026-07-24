using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface ISubProjectFinancialYearService
{
    Task<IReadOnlyList<SubProjectFinancialYearDto>> GetForSubProjectAsync(int subProjectId, CancellationToken cancellationToken = default);

    Task<SubProjectFinancialYearDto> LinkAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken = default);

    Task UnlinkAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken = default);
}
