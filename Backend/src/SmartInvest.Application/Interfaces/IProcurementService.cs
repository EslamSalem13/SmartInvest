using SmartInvest.Application.DTOs;
using SmartInvest.Domain.Enums;

namespace SmartInvest.Application.Interfaces;

/// <summary>
/// مراحل الطرح للمشروع الفرعي (كراسة الشروط ← الإعلان ← فتح المظاريف ← التقييم الفني ← التقييم المالي ← العقد والترسية).
/// الإصدارات سجل تاريخي: تُضاف ولا تُعدَّل ولا تُحذف. كل مرحلة مقفولة حتى تكتمل المرحلة السابقة لها.
/// </summary>
public interface IProcurementService
{
    /// <param name="excludeMemoId">استبعاد مذكرة عرض بعينها من فحص التعارض (مكتملة/جارية) — تُستخدم عند
    /// تعديل مذكرة قائمة حتى لا تُعتبر المذكرة نفسها تعارضًا مع مشروعاتها.</param>
    Task<IReadOnlyList<ProcurementSubProjectListItemDto>> GetSubProjectsAsync(int? financialYearId = null, int? excludeMemoId = null, CancellationToken cancellationToken = default);

    Task<ProcurementOverviewDto> GetOverviewAsync(int subProjectId, CancellationToken cancellationToken = default);

    Task<ProcurementStageDetailDto> GetStageAsync(int subProjectId, ProcurementStage stage, CancellationToken cancellationToken = default);

    Task<ProcurementVersionDto> UploadVersionAsync(int subProjectId, ProcurementStage stage, UploadProcurementVersionDto dto, CancellationToken cancellationToken = default);

    Task<FileDownloadDto> DownloadFileAsync(int subProjectId, ProcurementStage stage, int versionNumber, string fileKey, CancellationToken cancellationToken = default);

    Task SetCompletionAsync(int subProjectId, ProcurementStage stage, bool isCompleted, CancellationToken cancellationToken = default);

    /// <summary>تأكيد/إلغاء تأكيد صرف الدفعة المقدمة 25% — خاص بمرحلة العقد والترسية فقط.</summary>
    Task SetAdvancePaymentDoneAsync(int subProjectId, bool done, CancellationToken cancellationToken = default);

    /// <summary>حفظ بيانات الترسية: الإسناد، الدفعة المقدمة، مدة التنفيذ، الشرط الجزائي.</summary>
    Task SetContractAwardDetailsAsync(int subProjectId, SetContractAwardDetailsDto dto, CancellationToken cancellationToken = default);

    /// <summary>تسجيل تسليم أرضية المشروع للمقاول — تبدأ عندها مدة التنفيذ.</summary>
    Task SetSiteHandoverAsync(int subProjectId, DateTime handoverDate, FileUploadDto proofFile, CancellationToken cancellationToken = default);

    Task<FileDownloadDto> DownloadSiteHandoverProofAsync(int subProjectId, CancellationToken cancellationToken = default);

    /// <summary>يحدّث إثبات صرف الدفعة المقدمة على الإصدار الحالي مباشرة — بلا حاجة لإعادة رفع أمر الإسناد والعقد.</summary>
    Task SetAdvancePaymentProofAsync(int subProjectId, FileUploadDto proofFile, CancellationToken cancellationToken = default);

    /// <summary>مدير التخطيط يحدد المدة القصوى لمرحلة قبل ظهور زر الفشل — غير متاحة لمرحلة الإعلان (قاعدتها ثابتة).</summary>
    Task SetStageDurationAsync(int subProjectId, ProcurementStage stage, int? durationDays, CancellationToken cancellationToken = default);

    /// <summary>تاريخ نشر الإعلان فعليًا — منه تُحسب مدة الـ15 يومًا الإلزامية.</summary>
    Task SetAnnouncementDateAsync(int subProjectId, DateTime announcementDate, CancellationToken cancellationToken = default);

    /// <summary>"هذه المرحلة غير لازمة للطرح" — تُعامَل كمكتملة فتفتح ما بعدها.</summary>
    Task SkipStageAsync(int subProjectId, ProcurementStage stage, string reason, CancellationToken cancellationToken = default);

    /// <summary>فشل مرحلة — يبطل اكتمالها وما بعدها، ويُسجِّل السبب. لا يحذف أي إصدار.</summary>
    Task FailStageAsync(int subProjectId, ProcurementStage stage, string reason, CancellationToken cancellationToken = default);
}
