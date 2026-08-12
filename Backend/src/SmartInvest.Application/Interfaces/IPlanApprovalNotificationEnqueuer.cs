namespace SmartInvest.Application.Interfaces;

public interface IPlanApprovalNotificationEnqueuer
{
    Task EnqueueAsync(Plan plan, string approvedByUserId, CancellationToken cancellationToken = default);
}
