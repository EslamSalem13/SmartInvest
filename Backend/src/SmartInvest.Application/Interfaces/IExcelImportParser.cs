using SmartInvest.Application.Services.Import;

namespace SmartInvest.Application.Interfaces;

public interface IExcelImportParser
{
    Task<ParsedImportFile> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default);
}
