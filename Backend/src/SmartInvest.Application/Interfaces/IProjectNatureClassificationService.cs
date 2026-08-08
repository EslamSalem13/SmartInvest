using SmartInvest.Application.Services.Import;

namespace SmartInvest.Application.Interfaces;

public interface IProjectNatureClassificationService
{
    /// <summary>يصنّف كل صف كـ«توريدات» أو «مقاولات» ويكتب النتيجة في row.ProjectNature مباشرة.</summary>
    Task ClassifyAsync(List<ParsedImportRow> rows, CancellationToken cancellationToken = default);
}
