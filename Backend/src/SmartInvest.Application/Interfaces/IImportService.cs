using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IImportService
{
    Task<ImportPreviewResultDto> PreviewAsync(Stream fileStream, CancellationToken cancellationToken = default);

    Task<ImportCommitResultDto> CommitAsync(ImportCommitDto dto, CancellationToken cancellationToken = default);
}
