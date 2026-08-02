# AI-Assisted Excel Import Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add AI-assisted header-typo tolerance and per-sub-project measurement extraction to the existing Excel import feature, with a staff review step before any extracted measurement is committed.

**Architecture:** A single new `IAiGatewayClient` (Infrastructure) wraps the ITI Bedrock-proxy HTTP endpoint. Two features consume it: (1) `ExcelImportParser`'s header matcher falls back to one AI call per file only when the deterministic scan can't map every column; (2) a new `MeasurementExtractionService` (Application) batches sub-project names to the AI during **preview**, returns extracted `(name, value, unit)` triples per row for staff to review/edit in the wizard, and the **edited** result is sent back at commit time (no second AI call). A new `MeasurementResolutionService` (Application) resolves/creates the Measurement+Unit records via the existing `IMeasurementService`/`ILookupService` and records values via the existing `SetValuesForSubProjectAsync` — no new EF/repository code needed for that part.

**Tech Stack:** .NET 10 (Onion architecture), `HttpClient` via `IHttpClientFactory`, Angular 21 standalone components/Signals. No new third-party packages.

## Global Constraints

- No automated test suite exists in this repo (established convention) — every task's verification is `dotnet build`/`ng build` + live manual verification through the running app, exactly as done for every prior task on this branch.
- AI gateway: `http://apiaccess.iti.net.eg/api/v1/student/chat`, `Authorization: Bearer <key>`, model id `anthropic.claude-sonnet-4-6`. Request: `{ "model_id": "...", "messages": [{"role":"user","content":"..."}], "system_prompt": "...", "max_tokens": N }`. Response: `{ "output_text": "...", ... }`.
- The API key is a secret: set via `dotnet user-secrets set "AiGateway:ApiKey" "<key>"` in `Backend/src/SmartInvest.API`, or the `AiGateway__ApiKey` environment variable in other environments. **Never** put the real key in `appsettings.json` or `appsettings.Development.json` (both are committed to git) — this repo's `.gitignore` explicitly calls out AI keys as one of the secrets that must stay out of source control.
- Commit discipline: **one commit per task** (not one per fix-review round) — batch any review feedback into a single follow-up commit per task, not several.
- Arabic UI copy matches the existing wizard's tone (short, direct, matches `excel-import-wizard.ts`'s existing strings).

---

### Task 1: AI Gateway client

**Files:**
- Create: `Backend/src/SmartInvest.Application/Interfaces/IAiGatewayClient.cs`
- Create: `Backend/src/SmartInvest.Infrastructure/Services/AiGatewayClient.cs`
- Create: `Backend/src/SmartInvest.Application/Common/Ai/AiGatewayOptions.cs`
- Modify: `Backend/src/SmartInvest.API/appsettings.json`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Produces: `IAiGatewayClient.CompleteAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken cancellationToken = default) : Task<string>` — returns the raw `output_text` string, or throws `SmartInvest.Application.Common.Exceptions.BusinessRuleException` with an Arabic message on any HTTP/parse failure. Tasks 2 and 3 both consume this exact signature.

- [ ] **Step 1: Create the options class**

```csharp
// Backend/src/SmartInvest.Application/Common/Ai/AiGatewayOptions.cs
namespace SmartInvest.Application.Common.Ai;

public class AiGatewayOptions
{
    public const string SectionName = "AiGateway";

    public string BaseUrl { get; set; } = string.Empty;
    public string ModelId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Create the interface**

```csharp
// Backend/src/SmartInvest.Application/Interfaces/IAiGatewayClient.cs
namespace SmartInvest.Application.Interfaces;

public interface IAiGatewayClient
{
    Task<string> CompleteAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Create the implementation**

```csharp
// Backend/src/SmartInvest.Infrastructure/Services/AiGatewayClient.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using SmartInvest.Application.Common.Ai;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.Infrastructure.Services;

public class AiGatewayClient : IAiGatewayClient
{
    private readonly HttpClient _httpClient;
    private readonly AiGatewayOptions _options;

    public AiGatewayClient(HttpClient httpClient, IOptions<AiGatewayOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    private record ChatMessage([property: JsonPropertyName("role")] string Role, [property: JsonPropertyName("content")] string Content);

    private record ChatRequest(
        [property: JsonPropertyName("model_id")] string ModelId,
        [property: JsonPropertyName("messages")] List<ChatMessage> Messages,
        [property: JsonPropertyName("system_prompt")] string SystemPrompt,
        [property: JsonPropertyName("max_tokens")] int MaxTokens);

    private record ChatResponse([property: JsonPropertyName("output_text")] string? OutputText);

    public async Task<string> CompleteAsync(string systemPrompt, string userMessage, int maxTokens, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new BusinessRuleException("مفتاح خدمة الذكاء الاصطناعي غير مُهيأ");
        }

        var request = new ChatRequest(_options.ModelId, new List<ChatMessage> { new("user", userMessage) }, systemPrompt, maxTokens);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/student/chat")
        {
            Content = JsonContent.Create(request),
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new BusinessRuleException($"تعذّر الاتصال بخدمة الذكاء الاصطناعي: {ex.Message}");
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new BusinessRuleException($"فشل طلب الذكاء الاصطناعي ({(int)httpResponse.StatusCode}): {body}");
        }

        ChatResponse? parsed;
        try
        {
            parsed = await httpResponse.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken);
        }
        catch (JsonException ex)
        {
            throw new BusinessRuleException($"تعذّر قراءة رد خدمة الذكاء الاصطناعي: {ex.Message}");
        }

        return parsed?.OutputText ?? throw new BusinessRuleException("رد خدمة الذكاء الاصطناعي فارغ");
    }
}
```

- [ ] **Step 4: Add config to appsettings.json** (non-secret values only — base URL and model id are fine to commit; the key is not)

Edit `Backend/src/SmartInvest.API/appsettings.json`, add after the `"Cors"` block:

```json
  "AiGateway": {
    "BaseUrl": "http://apiaccess.iti.net.eg/api/v1",
    "ModelId": "anthropic.claude-sonnet-4-6",
    "ApiKey": ""
  }
```

- [ ] **Step 5: Set the real key via user-secrets** (not committed)

```bash
cd Backend/src/SmartInvest.API
dotnet user-secrets init
dotnet user-secrets set "AiGateway:ApiKey" "<the real sbg_ key>"
```

Expected: `dotnet user-secrets list` shows `AiGateway:ApiKey = sbg_...`. This adds a `UserSecretsId` to `SmartInvest.API.csproj` and stores the key outside the repo (`%APPDATA%/Microsoft/UserSecrets/<id>/secrets.json` on Windows) — confirm via `git status` that `SmartInvest.API.csproj`'s diff is *only* the new `<UserSecretsId>` element, nothing secret.

- [ ] **Step 6: Register in DependencyInjection.cs**

`AddInfrastructure(this IServiceCollection services, IConfiguration configuration)` already takes `IConfiguration` (`DependencyInjection.cs:21`). Add inside its body:

```csharp
        services.Configure<AiGatewayOptions>(configuration.GetSection(AiGatewayOptions.SectionName));
        services.AddHttpClient<IAiGatewayClient, AiGatewayClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
```

Add `using SmartInvest.Application.Common.Ai;` and `using SmartInvest.Infrastructure.Services;` (the latter may already be present) to the top of the file.

- [ ] **Step 7: Build**

```bash
cd Backend && dotnet build
```
Expected: 0 errors.

- [ ] **Step 8: Live-verify the client**

Add a temporary throwaway test: in `Program.cs`, is not appropriate to touch for a one-off check. Instead verify via a minimal manual check — after building, this client has no caller yet, so verification here is: confirm the build is clean and `dotnet user-secrets list` shows the key. Full functional verification of the actual HTTP call happens in Task 2 (first real caller).

- [ ] **Step 9: Commit**

```bash
git add Backend/src/SmartInvest.Application/Interfaces/IAiGatewayClient.cs Backend/src/SmartInvest.Application/Common/Ai/AiGatewayOptions.cs Backend/src/SmartInvest.Infrastructure/Services/AiGatewayClient.cs Backend/src/SmartInvest.API/appsettings.json Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs Backend/src/SmartInvest.API/SmartInvest.API.csproj
git commit -m "feat: add AI gateway HTTP client for the ITI-hosted Bedrock proxy"
```

---

### Task 2: Header-typo AI fallback

**Files:**
- Modify: `Backend/src/SmartInvest.Infrastructure/Services/ExcelImportParser.cs`
- Modify: `Backend/src/SmartInvest.Application/Interfaces/IExcelImportParser.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/Import/ImportService.cs`

**Interfaces:**
- Consumes: `IAiGatewayClient.CompleteAsync(string, string, int, CancellationToken)` from Task 1.
- Produces: `IExcelImportParser.ParseAsync(Stream, CancellationToken) : Task<ParsedImportFile>` — **replaces** the old synchronous `Parse(Stream)`. Task 3/`ImportService.PreviewAsync` (already async) just needs `await`.

- [ ] **Step 1: Change the interface to async**

```csharp
// Backend/src/SmartInvest.Application/Interfaces/IExcelImportParser.cs
using SmartInvest.Application.Services.Import;

namespace SmartInvest.Application.Interfaces;

public interface IExcelImportParser
{
    Task<ParsedImportFile> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Rewrite ExcelImportParser with the AI fallback**

Replace the full contents of `Backend/src/SmartInvest.Infrastructure/Services/ExcelImportParser.cs`:

```csharp
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using SmartInvest.Application.Common.Exceptions;
using SmartInvest.Application.Interfaces;
using SmartInvest.Application.Services.Import;

namespace SmartInvest.Infrastructure.Services;

public class ExcelImportParser : IExcelImportParser
{
    private static readonly string[] ExpectedHeaders =
    {
        "البرنامج الرئيسي", "البرنامج الفرعي", "كود المشروع الرئيسى", "المشروع الرئيسى",
        "مستوى المشروع", "الجهة المنفذة", "المركز", "كود المشروع", "المشروع الفرعى",
        "المكوّن العيني", "بنك", "ذاتي", "الوحدة الحسابية",
    };

    // Real-world plan files are typed without tashkeel (e.g. "المكون العيني") even though
    // the reference header above carries a shadda ("المكوّن العيني") - matching is diacritic-
    // insensitive so a missing/extra harakah doesn't block recognizing the file's columns.
    private static readonly Regex ArabicDiacritics = new("[ً-ٰٟۖ-ۭ]", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> NormalizedToExpectedHeader =
        ExpectedHeaders.ToDictionary(Normalize, h => h);

    private readonly IAiGatewayClient _aiGatewayClient;

    public ExcelImportParser(IAiGatewayClient aiGatewayClient)
    {
        _aiGatewayClient = aiGatewayClient;
    }

    private static string Normalize(string text) => ArabicDiacritics.Replace(text, string.Empty).Trim();

    public async Task<ParsedImportFile> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.First();

        var columnIndexByHeader = await FindColumnsAsync(worksheet, cancellationToken);
        if (columnIndexByHeader == null)
        {
            throw new BusinessRuleException("تعذّر التعرف على أعمدة الملف — تأكد من رفع ملف الخطة الصحيح");
        }

        var headerRowNumber = columnIndexByHeader.Values.Min(c => c.RowNumber);

        var rows = new List<ParsedImportRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;

        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            var columnIndex = columnIndexByHeader.ToDictionary(kv => kv.Key, kv => kv.Value.ColumnNumber);

            var mainProjectName = GetText(row, columnIndex, "المشروع الرئيسى");
            var subProjectName = GetText(row, columnIndex, "المشروع الفرعى");
            if (string.IsNullOrWhiteSpace(mainProjectName) && string.IsNullOrWhiteSpace(subProjectName))
            {
                continue;
            }

            rows.Add(new ParsedImportRow
            {
                RowIndex = rowNumber,
                MainProgramName = GetText(row, columnIndex, "البرنامج الرئيسي"),
                SubProgramName = GetText(row, columnIndex, "البرنامج الفرعي"),
                MainProjectCode = GetText(row, columnIndex, "كود المشروع الرئيسى"),
                MainProjectName = mainProjectName,
                ProjectLevelName = GetText(row, columnIndex, "مستوى المشروع"),
                ExecutiveAgencyName = GetText(row, columnIndex, "الجهة المنفذة"),
                MarkazName = GetText(row, columnIndex, "المركز"),
                SubProjectCode = GetText(row, columnIndex, "كود المشروع"),
                SubProjectName = subProjectName,
                ComponentTypeName = GetText(row, columnIndex, "المكوّن العيني"),
                BankFunding = GetDecimal(row, columnIndex, "بنك"),
                SelfFunding = GetDecimal(row, columnIndex, "ذاتي"),
                AccountingUnitName = GetText(row, columnIndex, "الوحدة الحسابية"),
            });
        }

        if (rows.Count == 0)
        {
            throw new BusinessRuleException("لم يتم العثور على أي صفوف بيانات في الملف");
        }

        var codedCount = rows.Count(r => !string.IsNullOrWhiteSpace(r.SubProjectCode));
        ImportMode mode;
        if (codedCount == 0)
        {
            mode = ImportMode.Suggested;
        }
        else if (codedCount == rows.Count)
        {
            mode = ImportMode.Approved;
        }
        else
        {
            throw new BusinessRuleException(
                "الملف يحتوي على مشروعات بأكواد وأخرى بدون أكواد — يجب أن يكون الملف إما خطة مقترحة (بدون أكواد) أو خطة معتمدة (بكل الأكواد)");
        }

        return new ParsedImportFile { Mode = mode, Rows = rows };
    }

    /// <summary>
    /// Returns canonical header name -> (column, row) for every one of the 13 expected headers,
    /// or null if they can't all be resolved (deterministically or via the AI fallback).
    /// </summary>
    private async Task<Dictionary<string, (int ColumnNumber, int RowNumber)>?> FindColumnsAsync(IXLWorksheet worksheet, CancellationToken cancellationToken)
    {
        var lastRowToScan = Math.Min(10, worksheet.LastRowUsed()?.RowNumber() ?? 1);

        var bestRowNumber = -1;
        var bestMatches = new Dictionary<string, (int ColumnNumber, int RowNumber)>();
        var bestUnmatchedCells = new List<(string Text, int ColumnNumber)>();

        for (var rowNumber = 1; rowNumber <= lastRowToScan; rowNumber++)
        {
            var matches = new Dictionary<string, (int ColumnNumber, int RowNumber)>();
            var unmatchedCells = new List<(string Text, int ColumnNumber)>();

            foreach (var cell in worksheet.Row(rowNumber).CellsUsed())
            {
                var text = cell.GetString();
                var normalized = Normalize(text);
                if (NormalizedToExpectedHeader.TryGetValue(normalized, out var canonicalHeader))
                {
                    matches.TryAdd(canonicalHeader, (cell.Address.ColumnNumber, rowNumber));
                }
                else if (!string.IsNullOrWhiteSpace(text))
                {
                    unmatchedCells.Add((text.Trim(), cell.Address.ColumnNumber));
                }
            }

            if (matches.Count == ExpectedHeaders.Length)
            {
                return matches;
            }

            if (matches.Count > bestMatches.Count)
            {
                bestRowNumber = rowNumber;
                bestMatches = matches;
                bestUnmatchedCells = unmatchedCells;
            }
        }

        // No row matched all 13 deterministically. Only worth an AI call if the best candidate
        // row is clearly the header row with a few typos, not a completely different sheet.
        if (bestRowNumber == -1 || bestMatches.Count < ExpectedHeaders.Length / 2)
        {
            return null;
        }

        var stillUnmapped = ExpectedHeaders.Where(h => !bestMatches.ContainsKey(h)).ToList();
        var aiMapping = await ResolveHeadersWithAiAsync(bestUnmatchedCells.Select(c => c.Text).ToList(), stillUnmapped, cancellationToken);
        if (aiMapping == null)
        {
            return null;
        }

        foreach (var (text, columnNumber) in bestUnmatchedCells)
        {
            if (aiMapping.TryGetValue(text, out var canonicalHeader) && canonicalHeader != null && !bestMatches.ContainsKey(canonicalHeader))
            {
                bestMatches[canonicalHeader] = (columnNumber, bestRowNumber);
            }
        }

        return bestMatches.Count == ExpectedHeaders.Length ? bestMatches : null;
    }

    private async Task<Dictionary<string, string?>?> ResolveHeadersWithAiAsync(List<string> unmatchedCellTexts, List<string> unmappedCanonicalHeaders, CancellationToken cancellationToken)
    {
        const string systemPrompt = """
            أنت تُطابق أسماء أعمدة ملف بيانات مشروعات حكومية بصيغة عربية.
            سيُعطى لك مصفوفة JSON من نصوص أعمدة فعلية، ومصفوفة JSON من أسماء الأعمدة القياسية المحتملة
            (قد يختلف النص الفعلي عن القياسي بخطأ إملائي أو تشكيل ناقص أو زائد فقط).
            أعد فقط كائن JSON يربط كل نص عمود فعلي بأفضل عمود قياسي مطابق له، أو null إن لم يوجد تطابق معقول.
            لا تُضف أي نص أو تعليق آخر ولا صيغة Markdown - أعد JSON خام فقط.
            """;

        var userMessage = JsonSerializer.Serialize(new { actualHeaders = unmatchedCellTexts, canonicalHeaders = unmappedCanonicalHeaders });

        string outputText;
        try
        {
            outputText = await _aiGatewayClient.CompleteAsync(systemPrompt, userMessage, 500, cancellationToken);
        }
        catch (BusinessRuleException)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(StripMarkdownFences(outputText));
        }
        catch (JsonException)
        {
            return null;
        }
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

    private static string GetText(IXLRow row, Dictionary<string, int> columnIndexByHeader, string header)
    {
        if (!columnIndexByHeader.TryGetValue(header, out var columnIndex))
        {
            return string.Empty;
        }

        return row.Cell(columnIndex).GetString().Trim();
    }

    private static decimal GetDecimal(IXLRow row, Dictionary<string, int> columnIndexByHeader, string header)
    {
        if (!columnIndexByHeader.TryGetValue(header, out var columnIndex))
        {
            return 0m;
        }

        var cell = row.Cell(columnIndex);
        return cell.TryGetValue<decimal>(out var value) ? value : 0m;
    }
}
```

- [ ] **Step 3: Update the one caller**

In `Backend/src/SmartInvest.Application/Services/Import/ImportService.cs`, find:

```csharp
        var file = _parser.Parse(fileStream);
```

Replace with:

```csharp
        var file = await _parser.ParseAsync(fileStream, cancellationToken);
```

- [ ] **Step 4: Build**

```bash
cd Backend && dotnet build
```
Expected: 0 errors.

- [ ] **Step 5: Live-verify the deterministic path still works**

With `backend-api` and `frontend-dev` running, log in, open the Excel import wizard, upload a previously-working suggested-mode test file (e.g. one from earlier testing with exact headers). Confirm it still reaches the reconcile step normally — the fast path must be unaffected.

- [ ] **Step 6: Live-verify the AI fallback path**

Build a test file with a deliberately typo'd header — e.g. rename "المركز" to "المركزز" (extra letter) in one cell, keeping the other 12 headers correct. Upload it. Confirm:
1. The preview still succeeds (no "تعذّر التعرف على أعمدة الملف" error) — proves the AI call correctly mapped "المركزز" back to "المركز".
2. Check `backend-api`'s logs (or a debugger breakpoint) show exactly one call to `apiaccess.iti.net.eg` for this upload, not one per row.

Then build a second test file where the header row is genuinely unrecognizable (e.g. all 13 headers replaced with random English words). Confirm it still fails cleanly with the original error message and does **not** make an AI call (the `< half matched` guard) — check logs show no request to `apiaccess.iti.net.eg` for this second upload.

- [ ] **Step 7: Commit**

```bash
git add Backend/src/SmartInvest.Infrastructure/Services/ExcelImportParser.cs Backend/src/SmartInvest.Application/Interfaces/IExcelImportParser.cs Backend/src/SmartInvest.Application/Services/Import/ImportService.cs
git commit -m "feat: fall back to AI header matching when a single column can't be recognized"
```

---

### Task 3: Measurement extraction service + preview wiring

**Files:**
- Create: `Backend/src/SmartInvest.Application/Interfaces/IMeasurementExtractionService.cs`
- Create: `Backend/src/SmartInvest.Application/Services/Import/MeasurementExtractionService.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/ImportDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/Import/ImportService.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IAiGatewayClient.CompleteAsync` (Task 1); `ParsedImportRow` (existing, has `RowIndex`, `SubProjectName`).
- Produces:
  - `ExtractedMeasurementDto { string Name; decimal Value; string Unit; }`
  - `RowMeasurementPreviewDto { int RowIndex; string SubProjectName; List<ExtractedMeasurementDto> Measurements; }`
  - `ImportPreviewResultDto.RowMeasurements : List<RowMeasurementPreviewDto>` (new property on the existing DTO)
  - `IMeasurementExtractionService.ExtractAsync(List<ParsedImportRow> rows, CancellationToken cancellationToken = default) : Task<List<RowMeasurementPreviewDto>>` — Task 4/5/6 consume `RowMeasurementPreviewDto`'s shape (both here and mirrored on the frontend).

- [ ] **Step 1: Add the new DTOs**

In `Backend/src/SmartInvest.Application/DTOs/ImportDtos.cs`, add (anywhere in the file, e.g. right after `ImportPreviewResultDto`):

```csharp
public class ExtractedMeasurementDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class RowMeasurementPreviewDto
{
    public int RowIndex { get; set; }
    public string SubProjectName { get; set; } = string.Empty;
    public List<ExtractedMeasurementDto> Measurements { get; set; } = new();
}
```

Then add a property to the existing `ImportPreviewResultDto`:

```csharp
public class ImportPreviewResultDto
{
    public string ImportId { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public SuggestedImportPreviewDto? Suggested { get; set; }
    public ApprovedImportPreviewDto? Approved { get; set; }
    public List<RowMeasurementPreviewDto> RowMeasurements { get; set; } = new();
}
```

- [ ] **Step 2: Create the interface**

```csharp
// Backend/src/SmartInvest.Application/Interfaces/IMeasurementExtractionService.cs
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Services.Import;

namespace SmartInvest.Application.Interfaces;

public interface IMeasurementExtractionService
{
    Task<List<RowMeasurementPreviewDto>> ExtractAsync(List<ParsedImportRow> rows, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Implement the batched extraction**

```csharp
// Backend/src/SmartInvest.Application/Services/Import/MeasurementExtractionService.cs
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
        var byRowIndex = parsed.ToDictionary(r => r.RowIndex);
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
```

- [ ] **Step 4: Wire into ImportService.PreviewAsync**

In `Backend/src/SmartInvest.Application/Services/Import/ImportService.cs`, add a constructor dependency and call it. Current constructor + `PreviewAsync` look like this (from Task 2's edit):

```csharp
    public ImportService(
        IExcelImportParser parser,
        ImportSessionStore sessionStore,
        SuggestedPlanImportService suggestedService,
        ApprovedPlanImportService approvedService,
        ICurrentUserService currentUser)
```

Add `IMeasurementExtractionService measurementExtractionService` as a new parameter, store it in a new `_measurementExtractionService` field (following this class's existing field-naming convention), and in `PreviewAsync`, after `result.Approved = ...` / `result.Suggested = ...` is set but before `return result;`, add:

```csharp
        result.RowMeasurements = await _measurementExtractionService.ExtractAsync(file.Rows, cancellationToken);
```

- [ ] **Step 5: Register in DependencyInjection.cs**

In `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`, next to the existing `services.AddScoped<IExcelImportParser, ExcelImportParser>();` line, add:

```csharp
        services.AddScoped<IMeasurementExtractionService, MeasurementExtractionService>();
```

- [ ] **Step 6: Build**

```bash
cd Backend && dotnet build
```
Expected: 0 errors.

- [ ] **Step 7: Live-verify**

Upload the real 88-row approved-plan file used earlier in this session (`نسخة من الخطة المعتمدة 1.xlsx`, or reconstruct an equivalent multi-row test file if unavailable) through the wizard up to the preview response. Inspect the network response body for `POST /api/subprojects/import/preview` (matching this session's established verification technique — `read_network_requests` on the request, not just the rendered UI, since the frontend UI for this doesn't exist until Task 6):

1. Confirm `rowMeasurements` is present with one entry per data row.
2. Confirm the row containing `"...سيارة 30 طن وسيارة 50 طن و2 سيارة 6 طن..."` extracted three measurements matching the pattern from the design spec (`عدد`/`1`/`سيارة 30 طن`, etc — exact wording may vary slightly, judgment call by the model, but the *shape* — one triple per vehicle mention, count as value — must hold).
3. Confirm the number of AI calls in `backend-api` logs is `ceil(rowCount / 15)`, not one per row.

- [ ] **Step 8: Commit**

```bash
git add Backend/src/SmartInvest.Application/Interfaces/IMeasurementExtractionService.cs Backend/src/SmartInvest.Application/Services/Import/MeasurementExtractionService.cs Backend/src/SmartInvest.Application/DTOs/ImportDtos.cs Backend/src/SmartInvest.Application/Services/Import/ImportService.cs Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs
git commit -m "feat: extract candidate measurements from sub-project names during preview"
```

---

### Task 4: Measurement resolution + commit-time recording

**Files:**
- Create: `Backend/src/SmartInvest.Application/Interfaces/IMeasurementResolutionService.cs`
- Create: `Backend/src/SmartInvest.Application/Services/Import/MeasurementResolutionService.cs`
- Modify: `Backend/src/SmartInvest.Application/DTOs/ImportDtos.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/Import/SuggestedPlanImportService.cs`
- Modify: `Backend/src/SmartInvest.Application/Services/Import/ApprovedPlanImportService.cs`
- Modify: `Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs`

**Interfaces:**
- Consumes: `IMeasurementService` and `ILookupService` (both already exist and are already registered — check `DependencyInjection.cs`, they're used elsewhere in this codebase's Application services).
- Produces:
  - `RowMeasurementResolutionDto { int RowIndex; List<ExtractedMeasurementDto> Measurements; }` (uses `ExtractedMeasurementDto` from Task 3) added to `ImportCommitDto` as `MeasurementResolutions`.
  - `IMeasurementResolutionService.RecordMeasurementsAsync(int subProjectId, int subProgramId, List<ExtractedMeasurementDto> measurements, CancellationToken cancellationToken = default) : Task` — Task 6 doesn't call this directly (it's wired into the two import services here), documented for completeness.

- [ ] **Step 1: Add the commit-side DTO**

In `Backend/src/SmartInvest.Application/DTOs/ImportDtos.cs`, add:

```csharp
public class RowMeasurementResolutionDto
{
    public int RowIndex { get; set; }
    public List<ExtractedMeasurementDto> Measurements { get; set; } = new();
}
```

Add a property to the existing `ImportCommitDto`:

```csharp
public class ImportCommitDto
{
    public string ImportId { get; set; } = string.Empty;
    public int FinancialYearId { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public List<ImportResolutionDto> MarkazResolutions { get; set; } = new();
    public List<ImportResolutionDto> MainProgramResolutions { get; set; } = new();
    public List<ImportResolutionDto> SubProgramResolutions { get; set; } = new();
    public List<ImportResolutionDto> AgencyResolutions { get; set; } = new();
    public List<ImportResolutionDto> ProjectLevelResolutions { get; set; } = new();
    public List<ImportResolutionDto> ComponentTypeResolutions { get; set; } = new();
    public List<ImportResolutionDto> AccountingUnitResolutions { get; set; } = new();
    public List<MainProjectCodeResolutionDto> MainProjectCodeResolutions { get; set; } = new();
    public List<ImportRowResolutionDto> RowResolutions { get; set; } = new();
    public List<RowMeasurementResolutionDto> MeasurementResolutions { get; set; } = new();
}
```

- [ ] **Step 2: Create the interface**

```csharp
// Backend/src/SmartInvest.Application/Interfaces/IMeasurementResolutionService.cs
using SmartInvest.Application.DTOs;

namespace SmartInvest.Application.Interfaces;

public interface IMeasurementResolutionService
{
    Task RecordMeasurementsAsync(int subProjectId, int subProgramId, List<ExtractedMeasurementDto> measurements, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Implement resolve-or-create + record**

```csharp
// Backend/src/SmartInvest.Application/Services/Import/MeasurementResolutionService.cs
using SmartInvest.Application.DTOs;
using SmartInvest.Application.Interfaces;

namespace SmartInvest.Application.Services.Import;

public class MeasurementResolutionService : IMeasurementResolutionService
{
    private readonly IMeasurementService _measurementService;
    private readonly ILookupService _lookupService;

    public MeasurementResolutionService(IMeasurementService measurementService, ILookupService lookupService)
    {
        _measurementService = measurementService;
        _lookupService = lookupService;
    }

    public async Task RecordMeasurementsAsync(int subProjectId, int subProgramId, List<ExtractedMeasurementDto> measurements, CancellationToken cancellationToken = default)
    {
        if (measurements.Count == 0)
        {
            return;
        }

        var values = new List<SetMeasurementValueDto>();
        foreach (var measurement in measurements)
        {
            var name = measurement.Name.Trim();
            var unitName = measurement.Unit.Trim();
            if (name.Length == 0 || unitName.Length == 0)
            {
                continue;
            }

            var unitId = await EnsureUnitAsync(unitName, cancellationToken);
            var measurementId = await EnsureMeasurementAsync(name, subProgramId, unitId, cancellationToken);

            values.Add(new SetMeasurementValueDto { MeasurementId = measurementId, UnitId = unitId, Value = measurement.Value });
        }

        if (values.Count > 0)
        {
            await _measurementService.SetValuesForSubProjectAsync(subProjectId, new SetSubProjectMeasurementValuesDto { Values = values }, cancellationToken);
        }
    }

    private async Task<int> EnsureUnitAsync(string unitName, CancellationToken cancellationToken)
    {
        var units = await _lookupService.GetUnitsAsync(cancellationToken);
        var existing = units.FirstOrDefault(u => u.Name.Trim() == unitName);
        if (existing != null)
        {
            return existing.Id;
        }

        var created = await _lookupService.CreateUnitAsync(new CreateNamedLookupDto { Name = unitName }, cancellationToken);
        return created.Id;
    }

    private async Task<int> EnsureMeasurementAsync(string measurementName, int subProgramId, int unitId, CancellationToken cancellationToken)
    {
        var all = await _measurementService.GetAllAsync(cancellationToken);
        var existing = all.FirstOrDefault(m => m.Name.Trim() == measurementName);

        if (existing == null)
        {
            var created = await _measurementService.CreateAsync(new CreateMeasurementDto
            {
                Name = measurementName,
                SubProgramIds = new List<int> { subProgramId },
                UnitIds = new List<int> { unitId },
            }, cancellationToken);
            return created.Id;
        }

        var needsSubProgram = !existing.SubProgramIds.Contains(subProgramId);
        var needsUnit = !existing.UnitIds.Contains(unitId);
        if (needsSubProgram || needsUnit)
        {
            await _measurementService.UpdateAsync(existing.Id, new UpdateMeasurementDto
            {
                Name = existing.Name,
                SubProgramIds = needsSubProgram ? existing.SubProgramIds.Append(subProgramId).ToList() : existing.SubProgramIds,
                UnitIds = needsUnit ? existing.UnitIds.Append(unitId).ToList() : existing.UnitIds,
            }, cancellationToken);
        }

        return existing.Id;
    }
}
```

- [ ] **Step 4: Wire into SuggestedPlanImportService**

In `Backend/src/SmartInvest.Application/Services/Import/SuggestedPlanImportService.cs`, add `IMeasurementResolutionService measurementResolutionService` as a new constructor parameter (after `unitOfWork`), store as `_measurementResolutionService` (declare the field alongside the existing `_unitOfWork` field).

In `CommitAsync`, the per-row loop currently ends with (lines 245-248):

```csharp
                    await _subProjectRepository.AddAsync(subProject, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    createdSubProjects.Add(subProject);
                    result.SubProjectsCreated++;
```

Insert the measurement call between `createdSubProjects.Add(subProject);` and `result.SubProjectsCreated++;`, still inside the same per-row `try` block (so a measurement failure is caught by the existing `catch` below and reported in `Failed` without rolling back the sub-project itself, matching this file's best-effort-per-row pattern):

```csharp
                    await _subProjectRepository.AddAsync(subProject, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    createdSubProjects.Add(subProject);

                    var measurementResolution = dto.MeasurementResolutions.FirstOrDefault(m => m.RowIndex == row.RowIndex);
                    if (measurementResolution != null)
                    {
                        await _measurementResolutionService.RecordMeasurementsAsync(subProject.SubProjectId, subProgramId, measurementResolution.Measurements, cancellationToken);
                    }

                    result.SubProjectsCreated++;
```

(`subProgramId` is already in scope here — it's the outer `foreach (var group in mainProjectGroups)` loop's resolved sub-program id, used a few lines above to build `mainProject`.)

- [ ] **Step 5: Wire into ApprovedPlanImportService**

In `Backend/src/SmartInvest.Application/Services/Import/ApprovedPlanImportService.cs`, add `IMeasurementResolutionService measurementResolutionService` as a new constructor parameter (after `unitOfWork`), store as `_measurementResolutionService`.

In `CommitAsync`'s per-row loop, all three branches (matched / resolved-to-existing / create-new) converge on the same line (currently line 220):

```csharp
                approvedSubProjectIds.Add(subProjectId);
```

Insert the measurement call immediately before it, still inside the same per-row `try` block:

```csharp
                var measurementResolution = dto.MeasurementResolutions.FirstOrDefault(m => m.RowIndex == row.RowIndex);
                if (measurementResolution != null)
                {
                    var subProgramId = mainProject?.SubProgramId
                        ?? (await _mainProjectRepository.GetByIdAsync((await _subProjectRepository.GetByIdAsync(subProjectId, cancellationToken))!.MainProjectId, cancellationToken))!.SubProgramId;
                    await _measurementResolutionService.RecordMeasurementsAsync(subProjectId, subProgramId, measurementResolution.Measurements, cancellationToken);
                }

                approvedSubProjectIds.Add(subProjectId);
```

`mainProject` (declared `MainProject? mainProject = null;` at the top of the loop body) is only non-null on the create-new branch; for the matched and resolved-to-existing branches, `mainProject?.SubProgramId` evaluates to `null` and the right side of `??` looks the sub-program up via the sub-project's own `MainProjectId` instead. Both `ISubProjectRepository.GetByIdAsync` and `IMainProjectRepository.GetByIdAsync` are inherited from `IGenericRepository<T>` (both repository interfaces extend it), and both are already injected into this class.

- [ ] **Step 6: Register in DependencyInjection.cs**

```csharp
        services.AddScoped<IMeasurementResolutionService, MeasurementResolutionService>();
```

- [ ] **Step 7: Build**

```bash
cd Backend && dotnet build
```
Expected: 0 errors.

- [ ] **Step 8: Live-verify via direct API calls** (frontend doesn't send `measurementResolutions` yet — that's Task 6, so drive this one via `fetch` in the browser console exactly as done earlier in this session for backend-only verification)

1. Preview a small approved-mode test file with one row whose name contains an obvious quantity (e.g. `"مشروع اختبار قياسات / سيارة 5 طن"` with code `MEAS-TEST-001`).
2. Take the `importId` and the `rowMeasurements` from the preview response.
3. POST to `/api/subprojects/import/commit` directly with `fetch`, including `measurementResolutions: [{ rowIndex: <the row's index>, measurements: [{name, value, unit}] }]` — either the AI's own extraction from the preview response, or a hand-crafted one if the AI didn't extract anything for this synthetic name.
4. After commit, `GET /api/subprojects/{id}` for the created/matched sub-project's id (from the commit response's counts, or by name search) — actually use `GET /api/measurements/subproject/{subProjectId}` (or whatever `GetValuesForSubProjectAsync`'s route is — check `MeasurementsController`) to confirm the value was recorded with the correct Measurement/Unit names and value.
5. Check `/api/settings/measurements` (or the equivalent lookup endpoint) to confirm a new Measurement/Unit was created if the names didn't already exist, scoped to the correct SubProgram.

- [ ] **Step 9: Commit**

```bash
git add Backend/src/SmartInvest.Application/Interfaces/IMeasurementResolutionService.cs Backend/src/SmartInvest.Application/Services/Import/MeasurementResolutionService.cs Backend/src/SmartInvest.Application/DTOs/ImportDtos.cs Backend/src/SmartInvest.Application/Services/Import/SuggestedPlanImportService.cs Backend/src/SmartInvest.Application/Services/Import/ApprovedPlanImportService.cs Backend/src/SmartInvest.Infrastructure/DependencyInjection.cs
git commit -m "feat: resolve/create Measurement+Unit and record values at import commit"
```

---

### Task 5: Frontend models + service

**Files:**
- Modify: `Frontend/src/app/core/models/project.models.ts`

**Interfaces:**
- Consumes: nothing new (mirrors Task 3/4's backend DTOs).
- Produces: `ExtractedMeasurement`, `RowMeasurementPreview`, `RowMeasurementResolution` — Task 6 imports these directly.

- [ ] **Step 1: Add the new interfaces**

In `Frontend/src/app/core/models/project.models.ts`, add near the existing `ImportPreviewResult`/`ImportCommit` interfaces:

```typescript
export interface ExtractedMeasurement {
  name: string;
  value: number;
  unit: string;
}

export interface RowMeasurementPreview {
  rowIndex: number;
  subProjectName: string;
  measurements: ExtractedMeasurement[];
}

export interface RowMeasurementResolution {
  rowIndex: number;
  measurements: ExtractedMeasurement[];
}
```

Extend the existing `ImportPreviewResult`:

```typescript
export interface ImportPreviewResult {
  importId: string;
  mode: 'Suggested' | 'Approved';
  suggested: SuggestedImportPreview | null;
  approved: ApprovedImportPreview | null;
  rowMeasurements: RowMeasurementPreview[];
}
```

Extend the existing `ImportCommit` (current shape, confirmed at `Frontend/src/app/core/models/project.models.ts:470-483`) by adding one line, `measurementResolutions: RowMeasurementResolution[];`, right after `rowResolutions`:

```typescript
export interface ImportCommit {
  importId: string;
  financialYearId: number;
  approvalDate?: string | null;
  markazResolutions: ImportResolution[];
  mainProgramResolutions: ImportResolution[];
  subProgramResolutions: ImportResolution[];
  agencyResolutions: ImportResolution[];
  projectLevelResolutions: ImportResolution[];
  componentTypeResolutions: ImportResolution[];
  accountingUnitResolutions: ImportResolution[];
  mainProjectCodeResolutions: MainProjectCodeResolution[];
  rowResolutions: ImportRowResolution[];
  measurementResolutions: RowMeasurementResolution[];
}
```

- [ ] **Step 2: Type-check**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json
```
Expected: no errors (nothing consumes the new fields yet, so this just confirms the interfaces themselves are syntactically valid and don't conflict with existing names).

- [ ] **Step 3: Commit**

```bash
git add Frontend/src/app/core/models/project.models.ts
git commit -m "feat: add frontend models for import measurement extraction/resolution"
```

---

### Task 6: Frontend review UI

**Files:**
- Modify: `Frontend/src/app/features/projects/excel-import-wizard.ts`

**Interfaces:**
- Consumes: `ExtractedMeasurement`, `RowMeasurementPreview`, `RowMeasurementResolution` (Task 5); `ImportPreviewResult.rowMeasurements`, `ImportCommit.measurementResolutions` (Task 5).
- Produces: nothing new for later tasks — this is the final UI piece.

- [ ] **Step 1: Add state for editable measurements**

In the `ExcelImportWizard` class body, add a signal holding the editable measurement resolutions, keyed by row index, mirroring the existing `rowResolutions` Map pattern already in this file:

```typescript
  private readonly measurementResolutions = new Map<number, ExtractedMeasurement[]>();
```

Add the import at the top of the file:

```typescript
import { ExtractedMeasurement, RowMeasurementPreview, ... } from '../../core/models/project.models';
```//merge into the existing import list from `project.models` rather than adding a second import line.

- [ ] **Step 2: Seed defaults on preview success**

In `submitUpload()`'s `next` handler (where `result.approved?.unresolvedRows` is currently seeded with `setRowCreateNew`), add, right after setting `this.preview.set(result)`:

```typescript
        this.measurementResolutions.clear();
        for (const rowPreview of result.rowMeasurements) {
          this.measurementResolutions.set(rowPreview.rowIndex, [...rowPreview.measurements]);
        }
```

(Clearing first matches this file's existing defensive pattern of clearing all resolution maps at the top of the preview-success handler, established in an earlier round of this same wizard's fixes.)

- [ ] **Step 3: Add helper methods**

```typescript
  protected measurementsForRow(rowIndex: number): ExtractedMeasurement[] {
    return this.measurementResolutions.get(rowIndex) ?? [];
  }

  protected updateMeasurement(rowIndex: number, index: number, field: keyof ExtractedMeasurement, value: string): void {
    const list = [...this.measurementsForRow(rowIndex)];
    const updated = { ...list[index] };
    if (field === 'value') {
      updated.value = Number(value) || 0;
    } else {
      (updated[field] as string) = value;
    }
    list[index] = updated;
    this.measurementResolutions.set(rowIndex, list);
  }

  protected removeMeasurement(rowIndex: number, index: number): void {
    const list = this.measurementsForRow(rowIndex).filter((_, i) => i !== index);
    this.measurementResolutions.set(rowIndex, list);
  }

  protected addMeasurement(rowIndex: number): void {
    const list = [...this.measurementsForRow(rowIndex), { name: '', value: 0, unit: '' }];
    this.measurementResolutions.set(rowIndex, list);
  }
```

- [ ] **Step 4: Add the review section to the confirm step template**

In the template, inside the `@if (step() === 'confirm') { ... }` block, after the existing summary `<p>` and (for approved mode) the approval-date field, add:

```html
              @if (preview()?.rowMeasurements && preview()!.rowMeasurements.length > 0) {
                <div class="si-fld full">
                  <label>القياسات المستخرَجة من أسماء المشروعات الفرعية (راجعها قبل التأكيد)</label>
                  @for (rowPreview of preview()!.rowMeasurements; track rowPreview.rowIndex) {
                    @if (measurementsForRow(rowPreview.rowIndex).length > 0 || true) {
                      <div class="recon-row" style="flex-direction:column; align-items:stretch;">
                        <span class="recon-name">{{ rowPreview.subProjectName }}</span>
                        @for (m of measurementsForRow(rowPreview.rowIndex); track $index) {
                          <div style="display:flex; gap:6px; margin-top:4px;">
                            <input type="text" [ngModel]="m.name" (ngModelChange)="updateMeasurement(rowPreview.rowIndex, $index, 'name', $event)" placeholder="اسم القياس" style="width:120px" />
                            <input type="number" [ngModel]="m.value" (ngModelChange)="updateMeasurement(rowPreview.rowIndex, $index, 'value', $event)" placeholder="القيمة" style="width:90px" />
                            <input type="text" [ngModel]="m.unit" (ngModelChange)="updateMeasurement(rowPreview.rowIndex, $index, 'unit', $event)" placeholder="الوحدة" style="width:140px" />
                            <button type="button" class="si-btn" (click)="removeMeasurement(rowPreview.rowIndex, $index)">حذف</button>
                          </div>
                        }
                        <button type="button" class="si-btn" style="align-self:flex-start; margin-top:4px;" (click)="addMeasurement(rowPreview.rowIndex)">+ إضافة قياس</button>
                      </div>
                    }
                  }
                </div>
              }
```

- [ ] **Step 5: Include in the commit payload**

In `submitCommit()`, where the `dto: ImportCommit` object is built, add:

```typescript
      measurementResolutions: [...this.measurementResolutions.entries()].map(([rowIndex, measurements]) => ({ rowIndex, measurements })),
```

- [ ] **Step 6: Clear on reset**

In the private `reset()` method, alongside the existing `this.rowResolutions.clear();` line, add:

```typescript
    this.measurementResolutions.clear();
```

- [ ] **Step 7: Type-check and build**

```bash
cd Frontend && npx tsc --noEmit -p tsconfig.app.json && npx ng build
```
Expected: 0 errors.

- [ ] **Step 8: Live-verify the full flow**

With both dev servers running:
1. Upload a real multi-row approved-mode file through the actual wizard UI (not raw `fetch` this time — full click-through).
2. On the confirm step, verify the extracted measurements render per row, are editable (change a value, remove one, add a manual one), and the approval-date field (approved mode) still works alongside this new section.
3. Submit. Verify the commit succeeds and the edited measurements (not the raw AI output) are what actually got recorded — pick one row where you edited a value before committing, then check that sub-project's recorded measurement value via `GET` matches your edit, not the AI's original number.
4. Confirm existing flows (suggested mode, a file with zero extractable measurements) still work unaffected — the review section simply doesn't render when `rowMeasurements` is empty.

- [ ] **Step 9: Commit**

```bash
git add Frontend/src/app/features/projects/excel-import-wizard.ts
git commit -m "feat: review and edit AI-extracted measurements before import commit"
```

---

### Task 7: Final end-to-end verification

**Files:** none (verification only).

- [ ] **Step 1: Full regression pass in the browser**

1. Suggested-mode import with a file that has zero extractable measurements — confirm identical behavior to before this plan (no measurement section shown, commit succeeds).
2. Approved-mode import of a real multi-row plan file with genuine measurement-bearing names — confirm main projects, sub-projects (with correct funding/details per the standalone fix already shipped), and measurements all land correctly; spot-check 2-3 sub-projects' recorded values against the source file by hand.
3. A file with one typo'd header — confirm it still imports successfully (Task 2's fallback).
4. A file with a genuinely wrong/unrecognizable header row — confirm it still fails cleanly with the original error, no AI call made.
5. Re-run the exact same file a second time — confirm the existing "no dedup" behavior is unaffected (fresh main/sub projects, same Suggested/Approved plan reused per financial year) and that measurements get recorded again for the fresh duplicate sub-projects too (not silently skipped).

- [ ] **Step 2: Confirm no stray console errors**

Use `read_console_messages` (`onlyErrors: true`) during the pass above.

- [ ] **Step 3: Final backend + frontend build**

```bash
cd Backend/src/SmartInvest.API && dotnet build
cd Frontend && npx ng build
```
Expected: both succeed with no errors.

- [ ] **Step 4: Final `git status` and `git log`**

```bash
git status
git log --oneline -10
```
Confirm only files touched by Tasks 1-6 show as modified/new in history, working tree clean, and exactly 7 commits landed for this plan (one per task, no stray fix-round commits) — if any task needed a follow-up fix, confirm it was folded into that task's single commit or squashed, not left as a separate small commit, per this plan's commit-discipline constraint.
