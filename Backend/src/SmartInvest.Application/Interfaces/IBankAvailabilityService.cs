using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IBankAvailabilityService
{
    Task<BankAvailabilityListDto> GetForFinancialYearAsync(int financialYearId, CancellationToken cancellationToken = default);

    Task<BankAvailabilityDto> CreateAsync(int financialYearId, CreateBankAvailabilityDto dto, CancellationToken cancellationToken = default);

    Task<BankAvailabilityDto> UpdateAsync(int financialYearId, int availabilityId, UpdateBankAvailabilityDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int financialYearId, int availabilityId, CancellationToken cancellationToken = default);

    Task<FileDownloadDto> DownloadDocumentAsync(int financialYearId, int availabilityId, int documentId, CancellationToken cancellationToken = default);
}
