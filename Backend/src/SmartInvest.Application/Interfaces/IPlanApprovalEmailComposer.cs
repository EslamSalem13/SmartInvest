using SmartInvest.Domain.Entities;

namespace SmartInvest.Application.Interfaces;

public record PlanApprovalEmailContent(string Subject, string HtmlTemplate, string PlainTextTemplate, bool AiGenerationUsed);

public interface IPlanApprovalEmailComposer
{
    Task<PlanApprovalEmailContent> ComposeAsync(
        PlanApprovalNotification notification,
        CancellationToken cancellationToken = default);
}
