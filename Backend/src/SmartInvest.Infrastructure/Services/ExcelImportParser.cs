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
    private static readonly Regex ArabicDiacritics = new("[ً-ٰٟۖ-ۭ]", RegexOptions.Compiled);

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
