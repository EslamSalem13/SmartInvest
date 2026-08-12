using ClosedXML.Excel;
using SmartInvest.Application.Common.Exceptions;

namespace SmartInvest.Infrastructure.Services;

internal enum ReportColumnKind
{
    Text,
    Integer,
    Decimal,
    MoneyThousands,
    Percentage,
    Date,
    DateTime,
    Boolean
}

internal class ReportColumn
{
    public string Header { get; set; } = string.Empty;
    public ReportColumnKind Kind { get; set; }
}

internal class ReportWorkbookData
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FilterDescription { get; set; } = string.Empty;
    public List<KeyValuePair<string, object?>> Summary { get; set; } = new();
    public List<ReportColumn> Columns { get; set; } = new();
    public List<object?[]> Rows { get; set; } = new();
}

internal class ReportSummaryValue
{
    public object? Value { get; set; }
    public ReportColumnKind Kind { get; set; }
}

internal static class ReportExcelBuilder
{
    public const int MaxRows = 10_000;

    private const string DarkGreen = "0B4F3A";
    private const string Green = "146B4A";
    private const string LightGreen = "E9F5EF";
    private const string Gold = "D7A93B";
    private const string Border = "CFDDD5";
    private const string Muted = "63756D";

    public static byte[] Build(ReportWorkbookData data)
    {
        if (data.Rows.Count > MaxRows)
        {
            throw new BusinessRuleException($"التقرير يحتوي على أكثر من {MaxRows:N0} صف. برجاء تضييق نطاق التقرير باستخدام السنة المالية أو عوامل التصفية.");
        }

        if (data.Columns.Count == 0)
        {
            throw new BusinessRuleException("تعريف أعمدة التقرير غير مكتمل");
        }

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SafeWorksheetName(data.Title));
        worksheet.RightToLeft = true;
        worksheet.TabColor = XLColor.FromHtml(Green);
        worksheet.ShowGridLines = false;

        var lastColumn = data.Columns.Count;
        var currentRow = 1;

        var titleRange = worksheet.Range(currentRow, 1, currentRow, lastColumn);
        titleRange.Merge();
        titleRange.Value = SafeText(data.Title);
        titleRange.Style.Fill.BackgroundColor = XLColor.FromHtml(DarkGreen);
        titleRange.Style.Font.FontColor = XLColor.White;
        titleRange.Style.Font.Bold = true;
        titleRange.Style.Font.FontSize = 18;
        titleRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        worksheet.Row(currentRow).Height = 34;
        currentRow++;

        var descriptionRange = worksheet.Range(currentRow, 1, currentRow, lastColumn);
        descriptionRange.Merge();
        descriptionRange.Value = SafeText(data.Description);
        descriptionRange.Style.Fill.BackgroundColor = XLColor.FromHtml(LightGreen);
        descriptionRange.Style.Font.FontColor = XLColor.FromHtml(DarkGreen);
        descriptionRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        descriptionRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        descriptionRange.Style.Alignment.WrapText = true;
        worksheet.Row(currentRow).Height = 30;
        currentRow++;

        WriteMetadataRow(worksheet, currentRow, lastColumn, "تاريخ إنشاء التقرير", DateTime.Now.ToString("yyyy/MM/dd HH:mm"));
        currentRow++;
        WriteMetadataRow(worksheet, currentRow, lastColumn, "نطاق التقرير", string.IsNullOrWhiteSpace(data.FilterDescription) ? "كل البيانات المتاحة" : data.FilterDescription);
        currentRow++;
        WriteMetadataRow(worksheet, currentRow, lastColumn, "عدد السجلات", data.Rows.Count.ToString("N0"));
        currentRow++;

        foreach (var item in data.Summary)
        {
            WriteMetadataRow(worksheet, currentRow, lastColumn, item.Key, FormatSummaryValue(item.Value));
            currentRow++;
        }

        currentRow++;
        var headerRow = currentRow;
        for (var columnIndex = 0; columnIndex < data.Columns.Count; columnIndex++)
        {
            var cell = worksheet.Cell(headerRow, columnIndex + 1);
            cell.Value = SafeText(data.Columns[columnIndex].Header);
        }

        var headerRange = worksheet.Range(headerRow, 1, headerRow, lastColumn);
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml(Green);
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Alignment.WrapText = true;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        headerRange.Style.Border.BottomBorderColor = XLColor.FromHtml(Gold);
        worksheet.Row(headerRow).Height = 34;

        for (var rowIndex = 0; rowIndex < data.Rows.Count; rowIndex++)
        {
            var values = data.Rows[rowIndex];
            if (values.Length != data.Columns.Count)
            {
                throw new BusinessRuleException("عدد قيم أحد صفوف التقرير لا يطابق عدد الأعمدة");
            }

            var excelRow = headerRow + rowIndex + 1;
            for (var columnIndex = 0; columnIndex < values.Length; columnIndex++)
            {
                WriteCell(worksheet.Cell(excelRow, columnIndex + 1), values[columnIndex], data.Columns[columnIndex].Kind);
            }

            var rowRange = worksheet.Range(excelRow, 1, excelRow, lastColumn);
            rowRange.Style.Fill.BackgroundColor = rowIndex % 2 == 0 ? XLColor.White : XLColor.FromHtml("F5FAF7");
            rowRange.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            rowRange.Style.Border.BottomBorderColor = XLColor.FromHtml(Border);
            rowRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        var lastDataRow = Math.Max(headerRow, headerRow + data.Rows.Count);
        var tableRange = worksheet.Range(headerRow, 1, lastDataRow, lastColumn);
        tableRange.Style.Border.LeftBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.RightBorder = XLBorderStyleValues.Thin;
        tableRange.Style.Border.LeftBorderColor = XLColor.FromHtml(Border);
        tableRange.Style.Border.RightBorderColor = XLColor.FromHtml(Border);
        if (data.Rows.Count > 0)
        {
            tableRange.SetAutoFilter();
        }

        worksheet.SheetView.FreezeRows(headerRow);
        worksheet.Columns(1, lastColumn).AdjustToContents(8, 45);
        foreach (var column in worksheet.Columns(1, lastColumn))
        {
            if (column.Width < 12)
            {
                column.Width = 12;
            }

            if (column.Width > 45)
            {
                column.Width = 45;
            }
        }

        if (data.Rows.Count == 0)
        {
            var emptyRow = headerRow + 1;
            var emptyRange = worksheet.Range(emptyRow, 1, emptyRow, lastColumn);
            emptyRange.Merge();
            emptyRange.Value = "لا توجد بيانات مطابقة لنطاق التقرير";
            emptyRange.Style.Fill.BackgroundColor = XLColor.FromHtml("FFF8E6");
            emptyRange.Style.Font.FontColor = XLColor.FromHtml(Muted);
            emptyRange.Style.Font.Italic = true;
            emptyRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Row(emptyRow).Height = 28;
        }

        worksheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        worksheet.PageSetup.FitToPages(1, 0);
        worksheet.PageSetup.Margins.Top = 0.4;
        worksheet.PageSetup.Margins.Bottom = 0.4;
        worksheet.PageSetup.Margins.Left = 0.25;
        worksheet.PageSetup.Margins.Right = 0.25;
        worksheet.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteMetadataRow(IXLWorksheet worksheet, int row, int lastColumn, string label, string value)
    {
        var splitColumn = Math.Min(2, lastColumn);
        var labelRange = worksheet.Range(row, 1, row, splitColumn);
        labelRange.Merge();
        labelRange.Value = SafeText(label);
        labelRange.Style.Font.Bold = true;
        labelRange.Style.Font.FontColor = XLColor.FromHtml(DarkGreen);
        labelRange.Style.Fill.BackgroundColor = XLColor.FromHtml("F3F7F5");

        if (splitColumn < lastColumn)
        {
            var valueRange = worksheet.Range(row, splitColumn + 1, row, lastColumn);
            valueRange.Merge();
            valueRange.Value = SafeText(value);
            valueRange.Style.Font.FontColor = XLColor.FromHtml(Muted);
            valueRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }
    }

    private static void WriteCell(IXLCell cell, object? value, ReportColumnKind kind)
    {
        if (value == null)
        {
            cell.Value = string.Empty;
            return;
        }

        switch (kind)
        {
            case ReportColumnKind.Integer:
                cell.Value = Convert.ToInt32(value);
                cell.Style.NumberFormat.Format = "#,##0";
                break;
            case ReportColumnKind.Decimal:
                cell.Value = Convert.ToDecimal(value);
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case ReportColumnKind.MoneyThousands:
                cell.Value = Convert.ToDecimal(value) / 1000m;
                cell.Style.NumberFormat.Format = "#,##0.00 \"ألف ج.م\"";
                break;
            case ReportColumnKind.Percentage:
                cell.Value = Convert.ToDecimal(value);
                cell.Style.NumberFormat.Format = "0.00\"%\"";
                break;
            case ReportColumnKind.Date:
                cell.Value = Convert.ToDateTime(value);
                cell.Style.DateFormat.Format = "yyyy/MM/dd";
                break;
            case ReportColumnKind.DateTime:
                cell.Value = Convert.ToDateTime(value);
                cell.Style.DateFormat.Format = "yyyy/MM/dd HH:mm";
                break;
            case ReportColumnKind.Boolean:
                cell.Value = Convert.ToBoolean(value) ? "نعم" : "لا";
                break;
            default:
                cell.Value = SafeText(Convert.ToString(value) ?? string.Empty);
                cell.Style.NumberFormat.Format = "@";
                cell.Style.Alignment.WrapText = true;
                break;
        }
    }

    private static string SafeText(string value)
    {
        var normalized = value.Replace("\0", string.Empty);
        var firstVisible = normalized.TrimStart();
        if (firstVisible.StartsWith('=') || firstVisible.StartsWith('+') || firstVisible.StartsWith('-') || firstVisible.StartsWith('@'))
        {
            return $"'{normalized}";
        }

        return normalized;
    }

    private static string FormatSummaryValue(object? value)
    {
        if (value is ReportSummaryValue typedValue)
        {
            if (typedValue.Value == null)
            {
                return "—";
            }

            if (typedValue.Kind == ReportColumnKind.MoneyThousands)
            {
                return $"{Convert.ToDecimal(typedValue.Value) / 1000m:N2} ألف ج.م";
            }

            if (typedValue.Kind == ReportColumnKind.Percentage)
            {
                return $"{Convert.ToDecimal(typedValue.Value):N2}%";
            }

            return Convert.ToString(typedValue.Value) ?? "—";
        }

        if (value == null)
        {
            return "—";
        }

        return Convert.ToString(value) ?? "—";
    }

    private static string SafeWorksheetName(string title)
    {
        var invalidCharacters = new[] { ':', '\\', '/', '?', '*', '[', ']' };
        var safe = title;
        foreach (var invalidCharacter in invalidCharacters)
        {
            safe = safe.Replace(invalidCharacter, '-');
        }

        safe = safe.Trim();
        if (safe.Length == 0)
        {
            safe = "تقرير";
        }

        return safe.Length <= 31 ? safe : safe[..31];
    }
}
