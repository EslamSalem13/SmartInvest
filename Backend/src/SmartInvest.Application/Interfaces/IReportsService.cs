using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IReportsService
{
    IReadOnlyList<ReportCatalogItemDto> GetCatalog();
    Task<FileDownloadDto> GenerateExcelAsync(string reportKey, int? financialYearId, CancellationToken cancellationToken = default);
    Task<FileDownloadDto> GenerateAiExcelAsync(string prompt, int? financialYearId, CancellationToken cancellationToken = default);
}
