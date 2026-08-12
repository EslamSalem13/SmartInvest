using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartInvest.Application.Common;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;
using SmartInvest.Domain.Enums;
using SmartInvest.Infrastructure.Data;

namespace SmartInvest.Infrastructure.Services;

public class PlanApprovalNotificationProcessor
{
    private readonly AppDbContext _context;
    private readonly IPlanApprovalEmailComposer _composer;
    private readonly IEmailService _emailService;
    private readonly PlanApprovalNotificationOptions _options;
    private readonly ILogger<PlanApprovalNotificationProcessor> _logger;

    public PlanApprovalNotificationProcessor(
        AppDbContext context,
        IPlanApprovalEmailComposer composer,
        IEmailService emailService,
        IOptions<PlanApprovalNotificationOptions> options,
        ILogger<PlanApprovalNotificationProcessor> logger)
    {
        _context = context;
        _composer = composer;
        _emailService = emailService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var ids = await _context.PlanApprovalNotifications
            .AsNoTracking()
            .Where(x =>
                x.Status != PlanApprovalNotificationStatus.Sent &&
                x.Status != PlanApprovalNotificationStatus.NoRecipients &&
                x.AttemptCount < _options.MaxAttempts &&
                (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now) &&
                (x.LockedUntilUtc == null || x.LockedUntilUtc < now))
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => x.PlanApprovalNotificationId)
            .Take(Math.Clamp(_options.BatchSize, 1, 100))
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
        {
            await ProcessOneAsync(id, cancellationToken);
        }

        return ids.Count;
    }

    private async Task ProcessOneAsync(int id, CancellationToken cancellationToken)
    {
        var notification = await _context.PlanApprovalNotifications
            .Include(x => x.Plan)
                .ThenInclude(x => x.FinancialYear)
            .Include(x => x.Recipients)
            .SingleOrDefaultAsync(x => x.PlanApprovalNotificationId == id, cancellationToken);

        if (notification is null || notification.Status is PlanApprovalNotificationStatus.Sent or PlanApprovalNotificationStatus.NoRecipients)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (notification.LockedUntilUtc >= now || notification.AttemptCount >= _options.MaxAttempts)
        {
            return;
        }

        notification.Status = PlanApprovalNotificationStatus.Processing;
        notification.ProcessingStartedAtUtc = now;
        notification.LockedUntilUtc = now.AddSeconds(Math.Clamp(_options.ProcessingLeaseSeconds, 30, 1800));
        notification.AttemptCount++;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(notification.Subject) || string.IsNullOrWhiteSpace(notification.HtmlBody))
            {
                var content = await _composer.ComposeAsync(notification, cancellationToken);
                notification.Subject = content.Subject;
                notification.HtmlBody = content.HtmlTemplate;
                notification.PlainTextBody = content.PlainTextTemplate;
                notification.AiGenerationUsed = content.AiGenerationUsed;
                await _context.SaveChangesAsync(cancellationToken);
            }

            foreach (var recipient in notification.Recipients.Where(x => x.Status != PlanApprovalRecipientStatus.Sent))
            {
                if (recipient.AttemptCount >= _options.MaxAttempts)
                {
                    continue;
                }

                recipient.AttemptCount++;
                try
                {
                    var safeName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(recipient.FullName) ? "مستخدم SmartInvest" : recipient.FullName);
                    var html = notification.HtmlBody!.Replace(PlanApprovalEmailComposer.RecipientNameMarker, safeName, StringComparison.Ordinal);
                    await _emailService.SendAsync(recipient.Email, notification.Subject!, html, cancellationToken);
                    recipient.Status = PlanApprovalRecipientStatus.Sent;
                    recipient.SentAtUtc = DateTime.UtcNow;
                    recipient.LastError = null;
                    _logger.LogInformation(
                        "Plan approval email sent. NotificationId={NotificationId}, RecipientId={RecipientId}",
                        notification.PlanApprovalNotificationId,
                        recipient.PlanApprovalNotificationRecipientId);
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    recipient.Status = PlanApprovalRecipientStatus.Failed;
                    recipient.LastError = Truncate(ex.Message, 2000);
                    _logger.LogWarning(
                        ex,
                        "Plan approval email failed. NotificationId={NotificationId}, RecipientId={RecipientId}",
                        notification.PlanApprovalNotificationId,
                        recipient.PlanApprovalNotificationRecipientId);
                }

                // Save each recipient immediately. SMTP has no transactional acknowledgement, so a process crash
                // after the provider accepts the message but before this save can still produce a duplicate on retry.
                await _context.SaveChangesAsync(cancellationToken);
            }

            CompleteAttempt(notification);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            notification.Status = notification.AttemptCount >= _options.MaxAttempts
                ? PlanApprovalNotificationStatus.Failed
                : PlanApprovalNotificationStatus.PartiallyFailed;
            notification.LastError = Truncate(ex.Message, 2000);
            notification.LockedUntilUtc = null;
            notification.NextAttemptAtUtc = notification.AttemptCount >= _options.MaxAttempts
                ? null
                : DateTime.UtcNow.Add(GetRetryDelay(notification.AttemptCount));
            await _context.SaveChangesAsync(CancellationToken.None);
            _logger.LogError(ex, "Plan approval notification processing failed. NotificationId={NotificationId}", id);
        }
    }

    private void CompleteAttempt(PlanApprovalNotification notification)
    {
        var allSent = notification.Recipients.All(x => x.Status == PlanApprovalRecipientStatus.Sent);
        var retryable = notification.Recipients.Any(x =>
            x.Status != PlanApprovalRecipientStatus.Sent && x.AttemptCount < _options.MaxAttempts);

        notification.LockedUntilUtc = null;
        notification.ProcessingStartedAtUtc = null;
        notification.LastError = notification.Recipients.FirstOrDefault(x => x.Status == PlanApprovalRecipientStatus.Failed)?.LastError;

        if (allSent)
        {
            notification.Status = PlanApprovalNotificationStatus.Sent;
            notification.CompletedAtUtc = DateTime.UtcNow;
            notification.NextAttemptAtUtc = null;
        }
        else if (retryable && notification.AttemptCount < _options.MaxAttempts)
        {
            notification.Status = PlanApprovalNotificationStatus.PartiallyFailed;
            notification.NextAttemptAtUtc = DateTime.UtcNow.Add(GetRetryDelay(notification.AttemptCount));
        }
        else
        {
            notification.Status = PlanApprovalNotificationStatus.Failed;
            notification.CompletedAtUtc = DateTime.UtcNow;
            notification.NextAttemptAtUtc = null;
        }
    }

    private TimeSpan GetRetryDelay(int attempt) => TimeSpan.FromSeconds(
        Math.Clamp(_options.InitialRetryDelaySeconds, 5, 3600) * Math.Pow(2, Math.Clamp(attempt - 1, 0, 8)));

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
