using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using SmartInvest.Application.Common.Ai;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.Application.Services.Import;

public class ProjectNatureClassificationService : IProjectNatureClassificationService
{
    private const int BatchSize = 25;

    // Same rationale as MeasurementExtractionService's cap - guards the shared AI gateway budget
    // against an abnormally large file rather than reflecting a real-world plan size.
    private const int MaxRowsForClassification = 300;

    private const string SystemPrompt = """
        أنت تصنّف مشروعات بنية تحتية حكومية مصرية إلى واحدة من فئتين بالضبط: «توريدات» أو «مقاولات».
        «توريدات» تعني المشروع أساسًا شراء/تجهيز أصول أو معدات جاهزة (مثل توريد سيارات، أثاث،
        أجهزة، مهمات، معدات) دون أعمال إنشاء أو تنفيذ ميداني كبيرة.
        «مقاولات» تعني المشروع أساسًا أعمال إنشاء أو تنفيذ أو رفع كفاءة ميدانية (مثل رصف طرق،
        إنشاء مبانٍ، تطوير شبكات، أعمال إنارة بالتركيب، عمرات وصيانة إنشائية).
        لكل اسم مشروع مُعطى، اختر الفئة الأصوب بناءً على الفعل والمحتوى الرئيسي في الاسم.
        أعد فقط مصفوفة JSON، بدون أي نص آخر، بدون صيغة Markdown، بهذا الشكل بالضبط:
        [{"rowIndex": <int>, "projectNature": "توريدات أو مقاولات"}]
        أدرج كل رقم صف مُعطى.
        """;

    private readonly IAiGatewayClient _aiGatewayClient;
    private readonly ILogger<ProjectNatureClassificationService> _logger;

    public ProjectNatureClassificationService(IAiGatewayClient aiGatewayClient, ILogger<ProjectNatureClassificationService> logger)
    {
        _aiGatewayClient = aiGatewayClient;
        _logger = logger;
    }

    private record RowInput([property: JsonPropertyName("rowIndex")] int RowIndex, [property: JsonPropertyName("subProjectName")] string SubProjectName);

    private record RowOutput([property: JsonPropertyName("rowIndex")] int RowIndex, [property: JsonPropertyName("projectNature")] string? ProjectNature);

    public async Task ClassifyAsync(List<ParsedImportRow> rows, CancellationToken cancellationToken = default)
    {
        if (rows.Count > MaxRowsForClassification)
        {
            _logger.LogWarning("Skipping AI project-nature classification: {RowCount} rows exceeds the cap of {MaxRows}", rows.Count, MaxRowsForClassification);
            return;
        }

        var byRowIndex = rows.ToDictionary(r => r.RowIndex);
        var batches = rows.Chunk(BatchSize).ToList();
        _logger.LogInformation("Issuing {BatchCount} AI project-nature-classification batch(es) for {RowCount} row(s)", batches.Count, rows.Count);

        foreach (var batch in batches)
        {
            await ClassifyBatchAsync(batch, byRowIndex, cancellationToken);
        }
    }

    private async Task ClassifyBatchAsync(ParsedImportRow[] batch, Dictionary<int, ParsedImportRow> byRowIndex, CancellationToken cancellationToken)
    {
        var input = batch.Select(r => new RowInput(r.RowIndex, r.SubProjectName)).ToList();
        var userMessage = JsonSerializer.Serialize(input);

        string outputText;
        try
        {
            outputText = await _aiGatewayClient.CompleteAsync(
                SystemPrompt,
                userMessage,
                1500,
                AiWorkload.ExcelImport,
                cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Degraded mode, same policy as measurement extraction: a failed batch just leaves
            // ProjectNature empty on those rows, it does not fail the import.
            LogDegraded(batch, "gateway call failed");
            return;
        }

        List<RowOutput>? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<List<RowOutput>>(AiResponseParsing.StripMarkdownFences(outputText), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Raw AI response for failed project-nature batch: {Raw}", outputText);
            LogDegraded(batch, "response JSON parse failure");
            return;
        }

        if (parsed == null)
        {
            LogDegraded(batch, "response deserialized to null");
            return;
        }

        foreach (var item in parsed)
        {
            if (!byRowIndex.TryGetValue(item.RowIndex, out var row))
            {
                continue;
            }

            // Only the two exact values the domain accepts - anything else (a hallucinated
            // variant, null, empty) is left as the row's existing empty default rather than
            // writing a value CreateSubProjectDtoValidator would never accept from the manual form.
            if (item.ProjectNature is "توريدات" or "مقاولات")
            {
                row.ProjectNature = item.ProjectNature;
            }
        }
    }

    private void LogDegraded(ParsedImportRow[] batch, string reason)
    {
        if (batch.Length == 0)
        {
            return;
        }

        var minRow = batch.Min(r => r.RowIndex);
        var maxRow = batch.Max(r => r.RowIndex);
        _logger.LogWarning("Project-nature classification batch (rows {MinRow}-{MaxRow}) fell back to degraded mode: {Reason}", minRow, maxRow, reason);
    }
}
