using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Common;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Enums;
using SmartInvest.Infrastructure.Data;
using SmartInvest.Infrastructure.Identity;

namespace SmartInvest.Infrastructure.Services;

public class PlanApprovalNotificationEnqueuer : IPlanApprovalNotificationEnqueuer
{
    private static readonly string[] RecipientRoles =
    [
        Roles.FinancialManager,
        Roles.FinancialEmployee,
        Roles.SuperAdmin,
    ];

    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public PlanApprovalNotificationEnqueuer(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task EnqueueAsync(Plan plan, string approvedByUserId, CancellationToken cancellationToken = default)
    {
        var alreadyQueued = await _context.PlanApprovalNotifications
            .AnyAsync(x => x.PlanId == plan.PlanId && x.EventType == "PlanApproved", cancellationToken);
        if (alreadyQueued)
        {
            return;
        }

        var approver = await _userManager.FindByIdAsync(approvedByUserId);
        var recipients = new Dictionary<string, (ApplicationUser User, string Role)>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in RecipientRoles)
        {
            foreach (var user in await _userManager.GetUsersInRoleAsync(role))
            {
                var email = user.Email?.Trim();
                if (!user.IsActive || string.IsNullOrWhiteSpace(email))
                {
                    continue;
                }

                try
                {
                    _ = new System.Net.Mail.MailAddress(email);
                }
                catch (FormatException)
                {
                    continue;
                }

                recipients.TryAdd(email, (user, role));
            }
        }

        var projects = plan.PlanProjects?
            .Where(link => link.SubProject != null)
            .Select(link => link.SubProject!)
            .GroupBy(project => project.SubProjectId)
            .Select(group => group.First())
            .ToList() ?? [];

        var availableFunding = await _context.BankAvailabilities
            .Where(x => x.FinancialYearId == plan.FinancialYearId)
            .SumAsync(x => (decimal?)x.Amount, cancellationToken) ?? 0m;

        var notification = new PlanApprovalNotification
        {
            PlanId = plan.PlanId,
            ApprovedByUserId = approvedByUserId,
            ApprovedByName = approver?.FullName ?? "مدير التخطيط",
            ProjectCount = projects.Count,
            BankFunding = projects.Sum(x => x.BankFunding),
            SelfFunding = projects.Sum(x => x.SelfFunding),
            AvailableFunding = availableFunding,
            Status = recipients.Count == 0
                ? PlanApprovalNotificationStatus.NoRecipients
                : PlanApprovalNotificationStatus.Pending,
            LastError = recipients.Count == 0
                ? "لا يوجد مستخدمون نشطون بأدوار الإدارة المالية أو السوبر أدمن ولديهم بريد صالح."
                : null,
        };

        foreach (var (email, recipient) in recipients)
        {
            notification.Recipients.Add(new PlanApprovalNotificationRecipient
            {
                UserId = recipient.User.Id,
                FullName = recipient.User.FullName,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                Role = recipient.Role,
            });
        }

        await _context.PlanApprovalNotifications.AddAsync(notification, cancellationToken);
    }
}
