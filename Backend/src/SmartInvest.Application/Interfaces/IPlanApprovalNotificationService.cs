using SmartInvest.Application.DTOs;
using SmartInvest.Application.DTOs.Common;
using SmartInvest.Domain.Enums;

namespace SmartInvest.Application.Interfaces;

public interface IPlanApprovalNotificationService
{
    Task<PagedResultDto<PlanApprovalNotificationListItemDto>> GetAsync(
        int page,
        int pageSize,
        PlanApprovalNotificationStatus? status,
        int? financialYearId,
        string? planName,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default);

    Task<PlanApprovalNotificationDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task RetryAsync(int id, CancellationToken cancellationToken = default);
}
