using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SmartInvest.Application.Common.Ai;
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.Application.Services.Import;

public class MeasurementExtractionService : IMeasurementExtractionService
{
    private const int BatchSize = 15;

    // A generous multiple of realistic real-world files (which run ~88 rows). Guards against a
    // compressed .xlsx smuggling far more than 15×N rows worth of short text past the 10MB
    // upload-size limit and exhausting the shared AI gateway budget in one preview.
    private const int MaxRowsForExtraction = 300;

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
        وحّد صيغة الوحدة دائمًا إلى الاسم الكامل غير المختصر، مهما كانت الصيغة الواردة في النص
        (مختصرة، برمز، أو بمسافة أو بدونها). أمثلة: "م" أو "م." → "متر"، "كم" → "كيلومتر"،
        "سم" → "سنتيمتر"، "م2" أو "م٢" أو "متر2" → "متر مربع"، "م3" أو "م٣" → "متر مكعب"،
        "كجم" أو "كغ" → "كيلوجرام"، "جم" أو "غ" → "جرام"، "ل" أو "لتر" → "لتر"، "ك" كبادئة قبل
        وحدة أخرى → "كيلو" + اسم الوحدة كاملًا (مثال: "كوات" → "كيلو وات"). إذا كانت الوحدة مذكورة
        بالفعل بصيغتها الكاملة، أبقها كما هي. لا تخترع وحدة غير مذكورة أو مُستنتَجة من السياق فقط.
        أعد فقط مصفوفة JSON، بدون أي نص آخر، بدون صيغة Markdown، بهذا الشكل بالضبط:
        [{"rowIndex": <int>, "measurements": [{"name": "<string>", "value": <number>, "unit": "<string>"}]}]
        أدرج كل رقم صف مُعطى، حتى لو كانت مصفوفة قياساته فارغة.
        """;

    private readonly IAiGatewayClient _aiGatewayClient;
    private readonly ILogger<MeasurementExtractionService> _logger;

    public MeasurementExtractionService(IAiGatewayClient aiGatewayClient, ILogger<MeasurementExtractionService> logger)
    {
        _aiGatewayClient = aiGatewayClient;
        _logger = logger;
    }

    private record RowInput([property: JsonPropertyName("rowIndex")] int RowIndex, [property: JsonPropertyName("subProjectName")] string SubProjectName);

    public async Task<List<RowMeasurementPreviewDto>> ExtractAsync(List<ParsedImportRow> rows, CancellationToken cancellationToken = default)
    {
        if (rows.Count > MaxRowsForExtraction)
        {
            _logger.LogWarning("Skipping AI measurement extraction: {RowCount} rows exceeds the cap of {MaxRows}", rows.Count, MaxRowsForExtraction);
            return rows.Select(r => new RowMeasurementPreviewDto { RowIndex = r.RowIndex, SubProjectName = r.SubProjectName }).ToList();
        }

        var result = new List<RowMeasurementPreviewDto>();
        var batches = rows.Chunk(BatchSize).ToList();
        _logger.LogInformation("Issuing {BatchCount} AI measurement-extraction batch(es) for {RowCount} row(s)", batches.Count, rows.Count);

        foreach (var batch in batches)
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
            outputText = await _aiGatewayClient.CompleteAsync(
                SystemPrompt,
                userMessage,
                2000,
                AiWorkload.ExcelImport,
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Degraded mode per spec §6: a failed batch just yields empty measurement lists,
            // it does not fail the import. Caller-initiated cancellation is excluded so it can
            // propagate normally instead of being masked as a degraded result.
            LogDegraded(batch, "gateway call failed");
            return fallback;
        }

        List<RowMeasurementPreviewDto>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<RowMeasurementPreviewDto>>(AiResponseParsing.StripMarkdownFences(outputText), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Raw AI response for failed batch: {Raw}", outputText);
            LogDegraded(batch, "response JSON parse failure");
            return fallback;
        }

        if (parsed == null)
        {
            LogDegraded(batch, "response deserialized to null");
            return fallback;
        }

        // Guarantee every input row appears exactly once, even if the model dropped one.
        var byRowIndex = parsed.GroupBy(r => r.RowIndex).ToDictionary(g => g.Key, g => g.First());
        return batch.Select(r => byRowIndex.TryGetValue(r.RowIndex, out var found)
                ? new RowMeasurementPreviewDto { RowIndex = r.RowIndex, SubProjectName = r.SubProjectName, Measurements = found.Measurements }
                : new RowMeasurementPreviewDto { RowIndex = r.RowIndex, SubProjectName = r.SubProjectName })
            .ToList();
    }

    private void LogDegraded(ParsedImportRow[] batch, string reason)
    {
        if (batch.Length == 0)
        {
            return;
        }

        var minRow = batch.Min(r => r.RowIndex);
        var maxRow = batch.Max(r => r.RowIndex);
        _logger.LogWarning("Measurement extraction batch (rows {MinRow}-{MaxRow}) fell back to degraded mode: {Reason}", minRow, maxRow, reason);
    }
}
