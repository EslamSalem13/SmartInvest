using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartInvest.Application.Common;
using SmartInvest.Application.Common.Ai;
using SmartInvest.Application.Interfaces;
using SmartInvest.Domain.Entities;

namespace SmartInvest.Infrastructure.Services;

public class PlanApprovalEmailComposer : IPlanApprovalEmailComposer
{
    public const string RecipientNameMarker = "{{RECIPIENT_NAME}}";

    private readonly IAiGatewayClient _aiGateway;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<PlanApprovalEmailComposer> _logger;

    public PlanApprovalEmailComposer(
        IAiGatewayClient aiGateway,
        IOptions<EmailOptions> emailOptions,
        ILogger<PlanApprovalEmailComposer> logger)
    {
        _aiGateway = aiGateway;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<PlanApprovalEmailContent> ComposeAsync(
        PlanApprovalNotification notification,
        CancellationToken cancellationToken = default)
    {
        var yearName = notification.Plan.FinancialYear?.Name ?? "السنة المالية الحالية";
        var subject = $"اعتماد الخطة الاستثمارية للعام المالي {yearName}";
        var intro = "تم اعتماد الخطة الاستثمارية رسميًا، ويمكن للإدارة المالية البدء في إجراءات التمويل والتنفيذ.";
        var summary = "يرجى مراجعة تفاصيل الخطة والمشروعات المعتمدة من خلال نظام SmartInvest.";
        var aiUsed = false;

        try
        {
            var response = await _aiGateway.CompleteAsync(
                "أنت مساعد مراسلات حكومية. أعد JSON فقط بالمفاتيح subject وintro وsummary. " +
                "اكتب عربية رسمية واضحة ومختصرة، ولا تضف أرقامًا أو معلومات غير الموجودة في البيانات.",
                JsonSerializer.Serialize(new
                {
                    eventName = "اعتماد الخطة الاستثمارية",
                    planName = notification.Plan.PlanName,
                    financialYear = yearName,
                    approvalDate = notification.Plan.ApprovalDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    approvedBy = notification.ApprovedByName,
                    projectCount = notification.ProjectCount,
                    bankFundingInThousands = notification.BankFunding / 1000m,
                    selfFundingInThousands = notification.SelfFunding / 1000m,
                    availableFundingInThousands = notification.AvailableFunding / 1000m,
                }),
                500,
                AiWorkload.PlanApprovalEmail,
                cancellationToken);

            var parsed = ParseAiResponse(response);
            if (parsed is not null)
            {
                subject = Limit(parsed.Subject, 300, subject);
                intro = Limit(parsed.Intro, 500, intro);
                summary = Limit(parsed.Summary, 700, summary);
                aiUsed = true;
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "AI email composition failed for plan {PlanId}; using deterministic fallback", notification.PlanId);
        }

        var safeSubject = WebUtility.HtmlEncode(subject);
        var safeIntro = WebUtility.HtmlEncode(intro);
        var safeSummary = WebUtility.HtmlEncode(summary);
        var safePlanName = WebUtility.HtmlEncode(notification.Plan.PlanName);
        var safeYearName = WebUtility.HtmlEncode(yearName);
        var safeApprover = WebUtility.HtmlEncode(notification.ApprovedByName);
        var planUrl = $"{_emailOptions.FrontendBaseUrl.TrimEnd('/')}/app/financial?financialYearId={notification.Plan.FinancialYearId}";
        var safeUrl = WebUtility.HtmlEncode(planUrl);

        var html = $$"""
            <!doctype html>
            <html lang="ar" dir="rtl">
            <body style="margin:0;background:#f2f6f3;font-family:Tahoma,Arial,sans-serif;color:#123c2f">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#f2f6f3;padding:28px 12px">
                <tr><td align="center">
                  <table role="presentation" width="640" cellpadding="0" cellspacing="0" style="max-width:640px;width:100%;background:#ffffff;border-radius:18px;overflow:hidden;border:1px solid #dce8e1">
                    <tr><td style="background:#0d5b3f;padding:24px 30px;color:#fff">
                      <div style="font-size:13px;opacity:.8">SmartInvest · محافظة المنوفية</div>
                      <h1 style="margin:8px 0 0;font-size:25px">{{safeSubject}}</h1>
                    </td></tr>
                    <tr><td style="padding:30px">
                      <p style="margin:0 0 14px;font-size:17px">مرحبًا {{RecipientNameMarker}}،</p>
                      <p style="margin:0 0 14px;line-height:1.9">{{safeIntro}}</p>
                      <table role="presentation" width="100%" cellpadding="9" cellspacing="0" style="background:#f7faf8;border-radius:12px;margin:18px 0">
                        <tr><td>الخطة</td><td style="font-weight:bold">{{safePlanName}}</td></tr>
                        <tr><td>السنة المالية</td><td style="font-weight:bold">{{safeYearName}}</td></tr>
                        <tr><td>عدد المشروعات</td><td style="font-weight:bold">{{notification.ProjectCount}}</td></tr>
                        <tr><td>تمويل بنكي</td><td style="font-weight:bold">{{FormatThousands(notification.BankFunding)}} ألف ج.م</td></tr>
                        <tr><td>تمويل ذاتي</td><td style="font-weight:bold">{{FormatThousands(notification.SelfFunding)}} ألف ج.م</td></tr>
                        <tr><td>المتاح</td><td style="font-weight:bold">{{FormatThousands(notification.AvailableFunding)}} ألف ج.م</td></tr>
                        <tr><td>اعتمد بواسطة</td><td style="font-weight:bold">{{safeApprover}}</td></tr>
                      </table>
                      <p style="line-height:1.9">{{safeSummary}}</p>
                      <p style="margin:24px 0"><a href="{{safeUrl}}" style="display:inline-block;background:#0d5b3f;color:#fff;text-decoration:none;padding:13px 24px;border-radius:10px;font-weight:bold">بدء العمل على الخطة</a></p>
                      <p style="margin:22px 0 0;color:#6c7f76;font-size:12px">رسالة آلية من نظام SmartInvest، برجاء عدم الرد عليها.</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        var plainText = $"مرحبًا {RecipientNameMarker}\n\n{intro}\n\nالخطة: {notification.Plan.PlanName}\nالسنة المالية: {yearName}\nعدد المشروعات: {notification.ProjectCount}\nتمويل بنكي: {FormatThousands(notification.BankFunding)} ألف ج.م\nتمويل ذاتي: {FormatThousands(notification.SelfFunding)} ألف ج.م\nالمتاح: {FormatThousands(notification.AvailableFunding)} ألف ج.م\n\n{summary}\n{planUrl}";

        return new PlanApprovalEmailContent(subject, html, plainText, aiUsed);
    }

    private static AiEmailDraft? ParseAiResponse(string raw)
    {
        var trimmed = raw.Trim();
        var firstBrace = trimmed.IndexOf('{');
        var lastBrace = trimmed.LastIndexOf('}');
        if (firstBrace < 0 || lastBrace <= firstBrace)
        {
            return null;
        }

        try
        {
            var draft = JsonSerializer.Deserialize<AiEmailDraft>(
                trimmed[firstBrace..(lastBrace + 1)],
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return string.IsNullOrWhiteSpace(draft?.Subject) ||
                   string.IsNullOrWhiteSpace(draft.Intro) ||
                   string.IsNullOrWhiteSpace(draft.Summary)
                ? null
                : draft;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Limit(string? value, int maxLength, string fallback)
    {
        var normalized = value?.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return fallback;
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string FormatThousands(decimal amount) =>
        (amount / 1000m).ToString("N2", CultureInfo.GetCultureInfo("ar-EG"));

    private sealed class AiEmailDraft
    {
        public string Subject { get; set; } = string.Empty;
        public string Intro { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
