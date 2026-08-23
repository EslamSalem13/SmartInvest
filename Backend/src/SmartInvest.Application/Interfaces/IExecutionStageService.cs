using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

/// <summary>
/// مراحل التنفيذ بعد الترسية (متابعة المشروعات) — منفصلة عن IProcurementService (مراحل الطرح قبل الترسية).
/// </summary>
public interface IExecutionStageService
{
    Task<IReadOnlyList<ExecutionStageDto>> GetBySubProjectAsync(int subProjectId, int financialYearId, CancellationToken cancellationToken = default);

    Task<ExecutionStageDto> CreateAsync(int subProjectId, CreateExecutionStageDto dto, CancellationToken cancellationToken = default);

    Task<ExecutionStageDto> UpdateAsync(int subProjectId, int stageId, UpdateExecutionStageDto dto, CancellationToken cancellationToken = default);

    Task<ExecutionStageDto> MarkCompleteAsync(int subProjectId, int stageId, int financialYearId, CancellationToken cancellationToken = default);

    /// <summary>عكس إنهاء المرحلة عن طريق الخطأ — يُلغي IsCompleted وتاريخ الإكمال، ولا يمس أي بيانات مسجَّلة أخرى.</summary>
    Task<ExecutionStageDto> ReopenAsync(int subProjectId, int stageId, int financialYearId, CancellationToken cancellationToken = default);

    Task<ExecutionStageDto> SetPenaltyAsync(int subProjectId, int stageId, int financialYearId, SetExecutionStagePenaltyDto dto, CancellationToken cancellationToken = default);

    Task<FileDownloadDto> DownloadFileAsync(int subProjectId, int stageId, int financialYearId, string fileKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FollowUpListItemDto>> GetFollowUpListAsync(
        int? financialYearId, int? mainProgramId, int? subProgramId, int? markazId, int? priorityId,
        string? searchTerm, CancellationToken cancellationToken = default);

    Task<ProjectCompletionEligibilityDto> GetCompletionEligibilityAsync(
        int subProjectId, int financialYearId, CancellationToken cancellationToken = default);

    /// <summary>
    /// خط زمني حياة المشروع الكامل عبر كل الدورات المالية (لمخطط لوحة التحكم) — بلا financialYearId
    /// عمدًا، فالمخطط لا يتقيّد بالسنة المختارة في لوحة التحكم.
    /// </summary>
    Task<ExecutionTimelineDto> GetExecutionTimelineAsync(int subProjectId, CancellationToken cancellationToken = default);

    Task<ProjectCompletionEligibilityDto> CompleteExecutionAsync(
        int subProjectId, int financialYearId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ينشئ/يحدّث مرحلة التسليم النهائي المُدارة تلقائيًا لهذا المشروع. آمن للاستدعاء المتكرر —
    /// لا يُنشئ صفًا مكررًا ولا يمس ما سُجِّل على الصف من صرف أو نسبة تنفيذ أو غرامة.
    /// لا يفعل شيئًا إذا لم تكن الترسية مكتملة.
    /// </summary>
    Task SyncFinalDeliveryStageAsync(int subProjectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// ينشئ/يحدّث مرحلة الدفعة المقدمة المُدارة تلقائيًا من بيانات العقد والترسية (القيمة والتاريخ
    /// وإثبات الصرف). آمن للاستدعاء المتكرر، ويحذف الصف إذا أُلغي تأكيد صرف الدفعة — الإثبات نفسه
    /// يبقى دائمًا في إصدارات العقد والترسية ولا يُنسخ إلى مرحلة التنفيذ.
    /// </summary>
    Task SyncAdvancePaymentStageAsync(int subProjectId, CancellationToken cancellationToken = default);
}
