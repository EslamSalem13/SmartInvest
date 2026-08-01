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

    public ParsedImportFile Parse(Stream fileStream)
    {
        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.First();

        var headerRowNumber = FindHeaderRow(worksheet);
        if (headerRowNumber == -1)
        {
            throw new BusinessRuleException("تعذّر التعرف على أعمدة الملف — تأكد من رفع ملف الخطة الصحيح");
        }

        var columnIndexByHeader = new Dictionary<string, int>();
        var headerRow = worksheet.Row(headerRowNumber);
        foreach (var cell in headerRow.CellsUsed())
        {
            var text = cell.GetString().Trim();
            if (ExpectedHeaders.Contains(text) && !columnIndexByHeader.ContainsKey(text))
            {
                columnIndexByHeader[text] = cell.Address.ColumnNumber;
            }
        }

        var rows = new List<ParsedImportRow>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRowNumber;

        for (var rowNumber = headerRowNumber + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            if (row.IsEmpty())
            {
                continue;
            }

            var mainProjectName = GetText(row, columnIndexByHeader, "المشروع الرئيسى");
            var subProjectName = GetText(row, columnIndexByHeader, "المشروع الفرعى");
            if (string.IsNullOrWhiteSpace(mainProjectName) && string.IsNullOrWhiteSpace(subProjectName))
            {
                continue;
            }

            rows.Add(new ParsedImportRow
            {
                RowIndex = rowNumber,
                MainProgramName = GetText(row, columnIndexByHeader, "البرنامج الرئيسي"),
                SubProgramName = GetText(row, columnIndexByHeader, "البرنامج الفرعي"),
                MainProjectCode = GetText(row, columnIndexByHeader, "كود المشروع الرئيسى"),
                MainProjectName = mainProjectName,
                ProjectLevelName = GetText(row, columnIndexByHeader, "مستوى المشروع"),
                ExecutiveAgencyName = GetText(row, columnIndexByHeader, "الجهة المنفذة"),
                MarkazName = GetText(row, columnIndexByHeader, "المركز"),
                SubProjectCode = GetText(row, columnIndexByHeader, "كود المشروع"),
                SubProjectName = subProjectName,
                ComponentTypeName = GetText(row, columnIndexByHeader, "المكوّن العيني"),
                BankFunding = GetDecimal(row, columnIndexByHeader, "بنك"),
                SelfFunding = GetDecimal(row, columnIndexByHeader, "ذاتي"),
                AccountingUnitName = GetText(row, columnIndexByHeader, "الوحدة الحسابية"),
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

    private static int FindHeaderRow(IXLWorksheet worksheet)
    {
        var lastRowToScan = Math.Min(10, worksheet.LastRowUsed()?.RowNumber() ?? 1);
        for (var rowNumber = 1; rowNumber <= lastRowToScan; rowNumber++)
        {
            var texts = worksheet.Row(rowNumber).CellsUsed().Select(c => c.GetString().Trim()).ToHashSet();
            if (ExpectedHeaders.All(h => texts.Contains(h)))
            {
                return rowNumber;
            }
        }

        return -1;
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
