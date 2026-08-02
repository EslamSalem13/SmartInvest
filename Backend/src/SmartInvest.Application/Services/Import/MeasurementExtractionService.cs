using System.Text.Json;
using System.Text.Json.Serialization;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.Application.Services.Import;

public class MeasurementExtractionService : IMeasurementExtractionService
{
    private const int BatchSize = 15;

    private const string SystemPrompt = """
        أنت تستخرج الكميات القابلة للقياس من أسماء مشروعات بنية تحتية حكومية بصيغة عربية.
        لكل اسم مشروع مُعطى، حدد كل عنصر قابل للقياس مذكور فيه (مثل عدد المركبات/المعدات حسب نوعها،
        الأبعاد الفيزيائية كالطول أو المساحة، السعات) وأعدها كثلاثيات (اسم القياس، القيمة، الوحدة).
        يمكن أن يُنتج اسم مشروع واحد صفر أو قياسًا واحدًا أو عدة قياسات.
        استخدم فهمك للسياق: عندما تذكر العبارة نوعًا أو مواصفة محددة لعنصر مع عدد ضمني أو صريح
        (مثل "سيارة 30 طن" = شاحنة سعة 30 طن، مع رقم اختياري في البداية للتعدد مثل "2 سيارة 6 طن")،
        يصبح العدد هو القيمة، و"عدد" هو اسم القياس، والعبارة الوصفية الكاملة (بما فيها مواصفتها،
        مثل "سيارة 30 طن") هي الوحدة. لنوع مختلف من الكميات (مثل طول طريق بالمتر، مساحة بالمتر
        المربع)، اختر اسم قياس عربي مناسب (مثل "طول" للطول) والوحدة المطابقة بدلًا من فرض "عدد" على كل شيء.
        أعد فقط مصفوفة JSON، بدون أي نص آخر، بدون صيغة Markdown، بهذا الشكل بالضبط:
        [{"rowIndex": <int>, "measurements": [{"name": "<string>", "value": <number>, "unit": "<string>"}]}]
        أدرج كل رقم صف مُعطى، حتى لو كانت مصفوفة قياساته فارغة.
        """;

    private readonly IAiGatewayClient _aiGatewayClient;

    public MeasurementExtractionService(IAiGatewayClient aiGatewayClient)
    {
        _aiGatewayClient = aiGatewayClient;
    }

    private record RowInput([property: JsonPropertyName("rowIndex")] int RowIndex, [property: JsonPropertyName("subProjectName")] string SubProjectName);

    public async Task<List<RowMeasurementPreviewDto>> ExtractAsync(List<ParsedImportRow> rows, CancellationToken cancellationToken = default)
    {
        var result = new List<RowMeasurementPreviewDto>();

        foreach (var batch in rows.Chunk(BatchSize))
        {
            var batchResult = await ExtractBatchAsync(batch, cancellationToken);
            result.AddRange(batchResult);
        }

        return result;
    }

    private async Task<List<RowMeasurementPreviewDto>> ExtractBatchAsync(ParsedImportRow[] batch, CancellationToken cancellationToken)
    {
        var fallback = batch.Select(r => new RowMeasurementPreviewDto { RowIndex = r.RowIndex, SubProjectName = r.SubProjectName }).ToList();

        var input = batch.Select(r => new RowInput(r.RowIndex, r.SubProjectName)).ToList();
        var userMessage = JsonSerializer.Serialize(input);

        string outputText;
        try
        {
            outputText = await _aiGatewayClient.CompleteAsync(SystemPrompt, userMessage, 2000, cancellationToken);
        }
        catch (Exception)
        {
            // Degraded mode per spec §6: a failed batch just yields empty measurement lists,
            // it does not fail the import.
            return fallback;
        }

        List<RowMeasurementPreviewDto>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<RowMeasurementPreviewDto>>(StripMarkdownFences(outputText), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return fallback;
        }

        if (parsed == null)
        {
            return fallback;
        }

        // Guarantee every input row appears exactly once, even if the model dropped one.
        var byRowIndex = parsed.GroupBy(r => r.RowIndex).ToDictionary(g => g.Key, g => g.First());
        return batch.Select(r => byRowIndex.TryGetValue(r.RowIndex, out var found)
                ? new RowMeasurementPreviewDto { RowIndex = r.RowIndex, SubProjectName = r.SubProjectName, Measurements = found.Measurements }
                : new RowMeasurementPreviewDto { RowIndex = r.RowIndex, SubProjectName = r.SubProjectName })
            .ToList();
    }

    private static string StripMarkdownFences(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
            {
                trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
            }
        }
        return trimmed;
    }
}
