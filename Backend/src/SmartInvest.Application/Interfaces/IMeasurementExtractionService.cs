using SmartInvest.Application.DTOs;
using SmartInvest.Application.Services.Import;

namespace SmartInvest.Application.Interfaces;

public interface IMeasurementExtractionService
{
    Task<List<RowMeasurementPreviewDto>> ExtractAsync(List<ParsedImportRow> rows, CancellationToken cancellationToken = default);
}
