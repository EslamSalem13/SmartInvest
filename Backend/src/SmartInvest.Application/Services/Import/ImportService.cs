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
    private readonly IProjectNatureClassificationService _projectNatureClassificationService;
    private readonly ILookupService _lookupService;
    private readonly ILookupMatchSuggestionService _lookupMatchSuggestionService;

    public ImportService(
        IExcelImportParser parser,
        ImportSessionStore sessionStore,
        SuggestedPlanImportService suggestedService,
        ApprovedPlanImportService approvedService,
        ICurrentUserService currentUser,
        IMeasurementExtractionService measurementExtractionService,
        IProjectNatureClassificationService projectNatureClassificationService,
        ILookupService lookupService,
        ILookupMatchSuggestionService lookupMatchSuggestionService)
    {
        _parser = parser;
        _sessionStore = sessionStore;
        _suggestedService = suggestedService;
        _approvedService = approvedService;
        _currentUser = currentUser;
        _measurementExtractionService = measurementExtractionService;
        _projectNatureClassificationService = projectNatureClassificationService;
        _lookupService = lookupService;
        _lookupMatchSuggestionService = lookupMatchSuggestionService;
    }

    public async Task<ImportPreviewResultDto> PreviewAsync(Stream fileStream, int? financialYearId, CancellationToken cancellationToken = default)
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
            result.Approved = await _approvedService.PreviewAsync(file, financialYearId, cancellationToken);
        }

        result.RowMeasurements = await _measurementExtractionService.ExtractAsync(file.Rows, cancellationToken);
        await NormalizeMeasurementUnitsAsync(result.RowMeasurements, cancellationToken);

        // Mutates file.Rows in place (sets ProjectNature) - those are the same row objects
        // ImportSessionStore just saved, so CommitAsync sees the classification without any DTO
        // needed to carry it across the preview/commit round trip.
        await _projectNatureClassificationService.ClassifyAsync(file.Rows, cancellationToken);

        return result;
    }

    // AI extraction decides each row's unit text independently, so the same physical unit can
    // come out as "م" on one row and "متر" on another - that fragments Unit records at commit
    // time (EnsureUnitAsync matches by exact name) and corrupts any reporting that sums by unit.
    // Canonicalize extracted unit names to an existing DB unit name before they ever reach the
    // reconcile screen or commit.
    private async Task NormalizeMeasurementUnitsAsync(List<RowMeasurementPreviewDto> rowMeasurements, CancellationToken cancellationToken)
    {
        var extractedUnitNames = rowMeasurements
            .SelectMany(r => r.Measurements)
            .Select(m => m.Unit.Trim())
            .Where(u => u.Length > 0)
            .Distinct()
            .ToList();
        if (extractedUnitNames.Count == 0)
        {
            return;
        }

        var existingUnitNames = (await _lookupService.GetUnitsAsync(cancellationToken)).Select(u => u.Name).ToList();
        var existingSet = existingUnitNames.ToHashSet();
        var unresolvedUnitNames = extractedUnitNames.Where(u => !existingSet.Contains(u)).ToList();
        if (unresolvedUnitNames.Count == 0)
        {
            return;
        }

        var suggestions = await _lookupMatchSuggestionService.SuggestMatchesAsync(
            new List<LookupMatchCategory> { new("measurementUnit", unresolvedUnitNames, existingUnitNames) },
            cancellationToken);

        if (!suggestions.TryGetValue("measurementUnit", out var matches) || matches.Count == 0)
        {
            return;
        }

        foreach (var row in rowMeasurements)
        {
            foreach (var measurement in row.Measurements)
            {
                var trimmed = measurement.Unit.Trim();
                if (matches.TryGetValue(trimmed, out var canonical) && canonical != null)
                {
                    measurement.Unit = canonical;
                }
            }
        }
    }

    public async Task<ImportCommitResultDto> CommitAsync(ImportCommitDto dto, CancellationToken cancellationToken = default)
    {
        var file = _sessionStore.Get(dto.ImportId)
            ?? throw new BusinessRuleException("انتهت صلاحية جلسة الاستيراد — برجاء رفع الملف مرة أخرى");

        if (file.Mode == ImportMode.Approved && _currentUser.Role is not Roles.PlanningManager and not Roles.SuperAdmin)
        {
            throw new ForbiddenAccessException("اعتماد المشروعات عن طريق الاستيراد يتطلب صلاحية مدير التخطيط");
        }

        if (_currentUser.Role is not Roles.PlanningManager and not Roles.SuperAdmin)
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
