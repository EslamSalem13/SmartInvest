using SmartInvest.Application.DTOs;
using SmartInvest.Domain.Enums;

namespace SmartInvest.Application.Interfaces;

/// <summary>
/// مراحل الطرح للمشروع الفرعي (كراسة الشروط ← الإعلان ← فتح المظاريف ← التقييم الفني ← التقييم المالي ← العقد والترسية).
/// الإصدارات سجل تاريخي: تُضاف ولا تُعدَّل ولا تُحذف. كل مرحلة مقفولة حتى تكتمل المرحلة السابقة لها.
/// </summary>
public interface IProcurementService
{
    Task<IReadOnlyList<ProcurementSubProjectListItemDto>> GetSubProjectsAsync(int? financialYearId = null, CancellationToken cancellationToken = default);

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
    Task SetSiteHandoverAsync(int subProjectId, DateTime handoverDate, CancellationToken cancellationToken = default);
}
