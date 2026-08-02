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
    private readonly IMeasurementExtractionService _measurementExtractionService;

    public ImportService(
        IExcelImportParser parser,
        ImportSessionStore sessionStore,
        SuggestedPlanImportService suggestedService,
        ApprovedPlanImportService approvedService,
        ICurrentUserService currentUser,
        IMeasurementExtractionService measurementExtractionService)
    {
        _parser = parser;
        _sessionStore = sessionStore;
        _suggestedService = suggestedService;
        _approvedService = approvedService;
        _currentUser = currentUser;
        _measurementExtractionService = measurementExtractionService;
    }

    public async Task<ImportPreviewResultDto> PreviewAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        var file = await _parser.ParseAsync(fileStream, cancellationToken);
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

        result.RowMeasurements = await _measurementExtractionService.ExtractAsync(file.Rows, cancellationToken);

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

        if (_currentUser.Role != Roles.PlanningManager)
        {
            // Recording AI-extracted measurements mutates global Measurement/Unit lookup records
            // (via MeasurementResolutionService), which is a PlanningManager-only capability.
            // A PlanningEmployee's commit must still succeed — just without auto-recorded measurements.
            dto.MeasurementResolutions = new List<RowMeasurementResolutionDto>();
        }

        var result = file.Mode == ImportMode.Suggested
            ? await _suggestedService.CommitAsync(file, dto, cancellationToken)
            : await _approvedService.CommitAsync(file, dto, cancellationToken);

        _sessionStore.Remove(dto.ImportId);

        return result;
    }
}
