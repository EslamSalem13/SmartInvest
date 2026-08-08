using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

/// <summary>مذكرات العرض — مذكرة واحدة قد تغطي عدة مشروعات فرعية.</summary>
public interface IPresentationMemoService
{
    Task<IReadOnlyList<PresentationMemoDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PresentationMemoDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<PresentationMemoDto> CreateAsync(CreatePresentationMemoDto dto, CancellationToken cancellationToken = default);

    Task<PresentationMemoDto> UpdateAsync(int id, UpdatePresentationMemoDto dto, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<ProcurementVersionDto> UploadVersionAsync(int id, UploadMemoVersionDto dto, CancellationToken cancellationToken = default);

    /// <summary><paramref name="fileKey"/>: "file" لملف المذكرة، "legal-affairs-decision" لقرار لجنة الشؤون القانونية.</summary>
    Task<FileDownloadDto> DownloadFileAsync(int id, int versionNumber, string? fileKey = null, CancellationToken cancellationToken = default);

    Task SetCompletionAsync(int id, bool isCompleted, CancellationToken cancellationToken = default);
}
