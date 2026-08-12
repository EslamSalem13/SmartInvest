using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.DTOs.Common;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Enums;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Services;

public class PlanApprovalNotificationService : IPlanApprovalNotificationService
{
    private readonly AppDbContext _context;

    public PlanApprovalNotificationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<PlanApprovalNotificationListItemDto>> GetAsync(
        int page,
        int pageSize,
        PlanApprovalNotificationStatus? status,
        int? financialYearId,
        string? planName,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _context.PlanApprovalNotifications.AsNoTracking();
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (financialYearId.HasValue) query = query.Where(x => x.Plan.FinancialYearId == financialYearId);
        if (!string.IsNullOrWhiteSpace(planName))
        {
            var term = planName.Trim();
            query = query.Where(x => x.Plan.PlanName.Contains(term));
        }
        if (fromUtc.HasValue) query = query.Where(x => x.CreatedAtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(x => x.CreatedAtUtc < toUtc.Value.Date.AddDays(1));

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PlanApprovalNotificationListItemDto
            {
                Id = x.PlanApprovalNotificationId,
                PlanId = x.PlanId,
                PlanName = x.Plan.PlanName,
                FinancialYearName = x.Plan.FinancialYear != null ? x.Plan.FinancialYear.Name : string.Empty,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                CompletedAtUtc = x.CompletedAtUtc,
                AttemptCount = x.AttemptCount,
                RecipientCount = x.Recipients.Count,
                SentCount = x.Recipients.Count(r => r.Status == PlanApprovalRecipientStatus.Sent),
                FailedCount = x.Recipients.Count(r => r.Status == PlanApprovalRecipientStatus.Failed),
                LastError = x.LastError,
            })
            .ToListAsync(cancellationToken);

        return new PagedResultDto<PlanApprovalNotificationListItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<PlanApprovalNotificationDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var result = await _context.PlanApprovalNotifications
            .AsNoTracking()
            .Where(x => x.PlanApprovalNotificationId == id)
            .Select(x => new PlanApprovalNotificationDetailDto
            {
                Id = x.PlanApprovalNotificationId,
                PlanId = x.PlanId,
                PlanName = x.Plan.PlanName,
                FinancialYearName = x.Plan.FinancialYear != null ? x.Plan.FinancialYear.Name : string.Empty,
                Status = x.Status,
                CreatedAtUtc = x.CreatedAtUtc,
                CompletedAtUtc = x.CompletedAtUtc,
                AttemptCount = x.AttemptCount,
                RecipientCount = x.Recipients.Count,
                SentCount = x.Recipients.Count(r => r.Status == PlanApprovalRecipientStatus.Sent),
                FailedCount = x.Recipients.Count(r => r.Status == PlanApprovalRecipientStatus.Failed),
                LastError = x.LastError,
                ApprovedByName = x.ApprovedByName,
                ProjectCount = x.ProjectCount,
                BankFunding = x.BankFunding,
                SelfFunding = x.SelfFunding,
                AvailableFunding = x.AvailableFunding,
                AiGenerationUsed = x.AiGenerationUsed,
                Subject = x.Subject,
                Recipients = x.Recipients
                    .OrderBy(r => r.FullName)
                    .Select(r => new PlanApprovalNotificationRecipientDto
                    {
                        Id = r.PlanApprovalNotificationRecipientId,
                        FullName = r.FullName,
                        Email = r.Email,
                        Role = r.Role,
                        Status = r.Status,
                        AttemptCount = r.AttemptCount,
                        SentAtUtc = r.SentAtUtc,
                        LastError = r.LastError,
                    }).ToList(),
            })
            .SingleOrDefaultAsync(cancellationToken);

        return result ?? throw new NotFoundException($"إشعار اعتماد الخطة رقم {id} غير موجود");
    }

    public async Task RetryAsync(int id, CancellationToken cancellationToken = default)
    {
        var notification = await _context.PlanApprovalNotifications
            .Include(x => x.Recipients)
            .SingleOrDefaultAsync(x => x.PlanApprovalNotificationId == id, cancellationToken)
            ?? throw new NotFoundException($"إشعار اعتماد الخطة رقم {id} غير موجود");

        var failed = notification.Recipients
            .Where(x => x.Status == PlanApprovalRecipientStatus.Failed)
            .ToList();
        if (failed.Count == 0)
        {
            throw new BusinessRuleException("لا يوجد مستلمون فشل إرسال الرسالة إليهم لإعادة المحاولة");
        }

        foreach (var recipient in failed)
        {
            recipient.Status = PlanApprovalRecipientStatus.Pending;
            recipient.AttemptCount = 0;
            recipient.LastError = null;
        }

        notification.Status = PlanApprovalNotificationStatus.Pending;
        notification.AttemptCount = 0;
        notification.CompletedAtUtc = null;
        notification.NextAttemptAtUtc = DateTime.UtcNow;
        notification.LockedUntilUtc = null;
        notification.ProcessingStartedAtUtc = null;
        notification.LastError = null;
        await _context.SaveChangesAsync(cancellationToken);
    }
}
