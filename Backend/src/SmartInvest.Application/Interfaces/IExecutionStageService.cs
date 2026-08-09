using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

/// <summary>
/// مراحل التنفيذ بعد الترسية (متابعة المشروعات) — منفصلة عن IProcurementService (مراحل الطرح قبل الترسية).
/// </summary>
public interface IExecutionStageService
{
    Task<IReadOnlyList<ExecutionStageDto>> GetBySubProjectAsync(int subProjectId, CancellationToken cancellationToken = default);

    Task<ExecutionStageDto> CreateAsync(int subProjectId, CreateExecutionStageDto dto, CancellationToken cancellationToken = default);

    Task<ExecutionStageDto> MarkCompleteAsync(int subProjectId, int stageId, CancellationToken cancellationToken = default);

    /// <summary>عكس إنهاء المرحلة عن طريق الخطأ — يُلغي IsCompleted وتاريخ الإكمال، ولا يمس أي بيانات مسجَّلة أخرى.</summary>
    Task<ExecutionStageDto> ReopenAsync(int subProjectId, int stageId, CancellationToken cancellationToken = default);

    Task<ExecutionStageDto> SetPenaltyAsync(int subProjectId, int stageId, SetExecutionStagePenaltyDto dto, CancellationToken cancellationToken = default);

    Task<FileDownloadDto> DownloadFileAsync(int subProjectId, int stageId, string fileKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FollowUpListItemDto>> GetFollowUpListAsync(
        int? financialYearId, int? mainProgramId, int? subProgramId, int? markazId, int? priorityId,
        string? searchTerm, CancellationToken cancellationToken = default);

    /// <summary>
    /// ينشئ/يحدّث مرحلة التسليم النهائي المُدارة تلقائيًا لهذا المشروع. آمن للاستدعاء المتكرر —
    /// لا يُنشئ صفًا مكررًا ولا يمس ما سُجِّل على الصف من صرف أو نسبة تنفيذ أو غرامة.
    /// لا يفعل شيئًا إذا لم تكن الترسية مكتملة.
    /// </summary>
    Task SyncFinalDeliveryStageAsync(int subProjectId, CancellationToken cancellationToken = default);
}
