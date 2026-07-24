using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IFinancialYearService
{
    Task<IReadOnlyList<FinancialYearDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FinancialYearDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<FinancialYearDto> CreateAsync(CreateFinancialYearDto dto, CancellationToken cancellationToken = default);

    Task<FinancialYearDto> UpdateAsync(int id, UpdateFinancialYearDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
