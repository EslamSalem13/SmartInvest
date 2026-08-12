namespace SmartInvest.Application.Common.Ai;

/// <summary>
/// كل قيمة هنا تقابل مزوّد ذكاء اصطناعي مدعوم من AiGatewayClient. لإضافة مزوّد جديد: أضف قيمة هنا
/// ثم فرع تنفيذ مطابق في AiGatewayClient.CompleteAsync.
/// </summary>
public enum AiProvider
{
    /// <summary>بروكسي ITI الوسيط (طلاب) — /student/chat بصيغة مخصصة.</summary>
    Iti,

    /// <summary>واجهة Anthropic الرسمية المباشرة (Messages API).</summary>
    Anthropic,

    /// <summary>واجهة Google Gemini الرسمية المباشرة (generateContent).</summary>
    Gemini,

    /// <summary>واجهة OpenAI الرسمية المباشرة (Chat Completions).</summary>
    OpenAi,
}

public enum AiWorkload
{
    Default,
    ExcelImport,
    Reports,
}

public class AiGatewayOptions
{
    public const string SectionName = "AiGateway";

    /// <summary>أي مزوّد يُستخدم فعليًا — غيّره في appsettings.Local.json فقط، بدون أي تعديل كود.</summary>
    public AiProvider Provider { get; set; } = AiProvider.Iti;

    /// <summary>
    /// اختياري — إن تُرك فارغًا يُستخدم العنوان الافتراضي الرسمي لكل مزوّد (انظر
    /// AiGatewayClient.GetDefaultBaseUrl). لازم تُملأ فقط لمزوّد ITI أو أي بروكسي وسيط مختلف.
    /// </summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string ModelId { get; set; } = string.Empty;
    public string ExcelImportModelId { get; set; } = string.Empty;
    public string ReportsModelId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
