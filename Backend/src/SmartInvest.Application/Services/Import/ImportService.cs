using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;

namespace SmartInvest.Application.Services.Import;

public class ImportService : IImportService
{
    private readonly IExcelImportParser _parser;
    private readonly ImportSessionStore _sessionStore;
    private readonly SuggestedPlanImportService _suggestedService;
    private readonly ApprovedPlanImportService _approvedService;
    private readonly ICurrentUserService _currentUser;

    public ImportService(
        IExcelImportParser parser,
        ImportSessionStore sessionStore,
        SuggestedPlanImportService suggestedService,
        ApprovedPlanImportService approvedService,
        ICurrentUserService currentUser)
    {
        _parser = parser;
        _sessionStore = sessionStore;
        _suggestedService = suggestedService;
        _approvedService = approvedService;
        _currentUser = currentUser;
    }

    public async Task<ImportPreviewResultDto> PreviewAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        var file = _parser.Parse(fileStream);
        var importId = _sessionStore.Save(file);

        var result = new ImportPreviewResultDto
        {
            ImportId = importId,
            Mode = file.Mode.ToString(),
        };

        if (file.Mode == ImportMode.Suggested)
        {
            result.Suggested = await _suggestedService.PreviewAsync(file, cancellationToken);
        }
        else
        {
            result.Approved = await _approvedService.PreviewAsync(file, cancellationToken);
        }

        return result;
    }

    public async Task<ImportCommitResultDto> CommitAsync(ImportCommitDto dto, CancellationToken cancellationToken = default)
    {
        var file = _sessionStore.Get(dto.ImportId)
            ?? throw new BusinessRuleException("انتهت صلاحية جلسة الاستيراد — برجاء رفع الملف مرة أخرى");

        if (file.Mode == ImportMode.Approved && _currentUser.Role != Roles.PlanningManager)
        {
            throw new ForbiddenAccessException("اعتماد المشروعات عن طريق الاستيراد يتطلب صلاحية مدير التخطيط");
        }

        var result = file.Mode == ImportMode.Suggested
            ? await _suggestedService.CommitAsync(file, dto, cancellationToken)
            : await _approvedService.CommitAsync(file, dto, cancellationToken);

        _sessionStore.Remove(dto.ImportId);

        return result;
    }
}
