using ClosedXML.Excel;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.Extensions.Localization;
using _02.Application.Resources;

namespace expense_tracker_backend.Application.Services;

/// <summary>Single-sheet budget workbook. Static labels use <c>ApplicationResource</c>; category names and other stored fields are unchanged.</summary>
public class BudgetReportWorkbookBuilder : IBudgetReportWorkbookBuilder
{
    // Match ExportLambda ExcelReportBuilder palette
    private static readonly XLColor HeaderBg = XLColor.FromHtml("#1F4E79");
    private static readonly XLColor HeaderFg = XLColor.White;
    private static readonly XLColor SubHeaderBg = XLColor.FromHtml("#D6E4F0");
    private static readonly XLColor IncomeBg = XLColor.FromHtml("#E2EFDA");
    private static readonly XLColor ExpenseBg = XLColor.FromHtml("#FCE4EC");
    private static readonly XLColor TotalRowBg = XLColor.FromHtml("#FFF2CC");
    private static readonly XLColor SummaryHighlight = XLColor.FromHtml("#2E75B6");
    private static readonly XLColor PositiveDiff = XLColor.FromHtml("#548235");
    private static readonly XLColor NegativeDiff = XLColor.FromHtml("#C00000");
    private static readonly XLColor Zebra = XLColor.FromHtml("#F5F5F5");
    private static readonly XLColor BorderGray = XLColor.FromHtml("#999999");
    /// <summary>Title text on white header (reference: Spendio-style report title).</summary>
    private static readonly XLColor ReportTitleBlue = XLColor.FromHtml("#2E75B6");

    private const int LastCol = 6;

    private readonly IStringLocalizer<ApplicationResource> _loc;

    public BudgetReportWorkbookBuilder(IStringLocalizer<ApplicationResource> loc)
    {
        _loc = loc;
    }

    private string S(string key) => _loc[key].Value;
    private string S(string key, params object[] args) => _loc[key, args].Value;

    public byte[] Build(Budget budget, string currencyCode, string? userDisplayName)
    {
        var currency = string.IsNullOrWhiteSpace(currencyCode) ? "JPY" : currencyCode.Trim().ToUpperInvariant();
        var fmt = CurrencyNumberFormat(currency);
        var innerTitle = string.IsNullOrWhiteSpace(userDisplayName)
            ? S("BudgetExcel_TitleDefault")
            : S("BudgetExcel_TitleWithName", userDisplayName.Trim());
        var mainTitle = S("BudgetExcel_MainTitleLine", innerTitle);

        var periodTypeLabel = budget.PeriodType switch
        {
            AppConstants.BudgetPeriodType.Monthly => S("BudgetExcel_PeriodType_Monthly"),
            AppConstants.BudgetPeriodType.Custom => S("BudgetExcel_PeriodType_Custom"),
            _ => budget.PeriodType.ToString()
        };

        using var wb = new XLWorkbook();
        wb.Style.Font.FontName = "Calibri";
        wb.Style.Font.FontSize = 11;

        var ws = wb.Worksheets.Add(S("BudgetExcel_SheetName"));
        var row = 1;

        // Report header (white band, blue title, grey rules — matches Spendio-style reference)
        var titleRange = ws.Range(row, 1, row, LastCol);
        titleRange.Merge();
        ws.Cell(row, 1).Value = mainTitle;
        StyleReportTitleRow(ws, titleRange);
        row++;

        var userForMeta = string.IsNullOrWhiteSpace(userDisplayName)
            ? S("BudgetExcel_HeaderUnknownUser")
            : userDisplayName.Trim();
        var metaRange = ws.Range(row, 1, row, LastCol);
        metaRange.Merge();
        ws.Cell(row, 1).Value = S("BudgetExcel_HeaderMetaLine", userForMeta, budget.StartDate, budget.EndDate, currency, periodTypeLabel);
        StyleReportMetaRow(ws, metaRange);
        row++;

        row++; // spacer before body

        var categories = budget.BudgetCategories.OrderBy(c => c.SortOrder).ToList();
        var totalAllocated = categories.Sum(c => c.AllocatedAmount);
        var totalSpent = categories.Sum(c => c.Snapshot?.SpentAmount ?? 0m);
        var remaining = budget.TotalAmount - totalSpent;
        var unallocatedToLines = budget.TotalAmount - totalAllocated;

        ws.Cell(row, 1).Value = S("BudgetExcel_BalanceOverview");
        StyleSectionHeader(ws.Range(row, 1, row, LastCol));
        row++;

        var balanceStart = row;
        WriteLabelValueRow(ws, ref row, S("BudgetExcel_TotalBudgetCeiling"), budget.TotalAmount, fmt, SubHeaderBg, SubHeaderBg);
        WriteLabelValueRow(ws, ref row, S("BudgetExcel_TotalAllocatedLines"), totalAllocated, fmt, SubHeaderBg, SubHeaderBg);
        WriteLabelValueRow(ws, ref row, S("BudgetExcel_UnallocatedToLines"), unallocatedToLines, fmt, SubHeaderBg, SubHeaderBg);
        WriteLabelValueRow(ws, ref row, S("BudgetExcel_TotalSpentActual"), totalSpent, fmt, ExpenseBg, ExpenseBg);
        WriteLabelValueRow(ws, ref row, S("BudgetExcel_RemainingBudgetMinusSpent"), remaining, fmt, IncomeBg, IncomeBg, colorValue: true);

        ws.Range(balanceStart, 1, row - 1, 2).Style.Font.SetBold(false);
        StyleBorderOutside(ws.Range(balanceStart - 1, 1, row - 1, LastCol));
        row += 2;

        ws.Cell(row, 1).Value = S("BudgetExcel_BudgetByCategory");
        StyleSectionHeader(ws.Range(row, 1, row, LastCol));
        row++;

        var unknown = S("BudgetExcel_UnknownCategory");
        var headers = new[]
        {
            S("BudgetExcel_ColumnCategory"),
            S("BudgetExcel_ColumnProjected", currency),
            S("BudgetExcel_ColumnActual", currency),
            S("BudgetExcel_ColumnDifference", currency),
            S("BudgetExcel_ColumnVariancePercent"),
            S("BudgetExcel_ColumnTxCount")
        };
        WriteHeaderRow(ws, row, headers);
        var headerRow = row;
        row++;

        var dataStart = row;
        foreach (var bc in categories)
        {
            var name = bc.Category?.DisplayName ?? unknown;
            var allocated = bc.AllocatedAmount;
            var spent = bc.Snapshot?.SpentAmount ?? 0m;
            var diff = allocated - spent;
            var pctRatio = allocated > 0 ? (double)(spent / allocated) : (double?)null;

            ws.Cell(row, 1).Value = name;
            SetCurrency(ws.Cell(row, 2), allocated, fmt);
            SetCurrency(ws.Cell(row, 3), spent, fmt);
            SetCurrency(ws.Cell(row, 4), diff, fmt);
            ColorDiff(ws.Cell(row, 4), diff);

            if (pctRatio.HasValue)
            {
                ws.Cell(row, 5).Value = pctRatio.Value;
                ws.Cell(row, 5).Style.NumberFormat.Format = "0.0%";
                if (pctRatio.Value > 1.0)
                    ws.Cell(row, 5).Style.Font.SetFontColor(NegativeDiff);
            }

            ws.Cell(row, 6).Value = bc.Snapshot?.TransactionCount ?? 0;
            ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            if ((row - dataStart) % 2 == 1)
                ws.Range(row, 1, row, LastCol).Style.Fill.SetBackgroundColor(Zebra);

            row++;
        }

        ws.Cell(row, 1).Value = S("BudgetExcel_Subtotal");
        ws.Cell(row, 1).Style.Font.SetBold(true);
        SetCurrency(ws.Cell(row, 2), totalAllocated, fmt);
        SetCurrency(ws.Cell(row, 3), totalSpent, fmt);
        SetCurrency(ws.Cell(row, 4), totalAllocated - totalSpent, fmt);
        ColorDiff(ws.Cell(row, 4), totalAllocated - totalSpent);
        ws.Cell(row, 5).Clear();
        ws.Cell(row, 6).Value = categories.Sum(c => c.Snapshot?.TransactionCount ?? 0);
        ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        ws.Range(row, 1, row, LastCol).Style.Fill.SetBackgroundColor(TotalRowBg).Font.SetBold(true);
        StyleBorderOutside(ws.Range(headerRow, 1, row, LastCol));

        row += 2;
        ws.Cell(row, 1).Value = S("BudgetExcel_FooterNote");
        ws.Range(row, 1, row, LastCol).Merge().Style
            .Font.SetItalic(true).Font.SetFontSize(10).Font.SetFontColor(XLColor.Gray)
            .Alignment.SetWrapText(true).Alignment.SetVertical(XLAlignmentVerticalValues.Top);
        ws.Row(row).Height = 36;

        ws.Columns(1, LastCol).AdjustToContents();
        ws.Column(1).Width = Math.Max(ws.Column(1).Width, 28);
        for (var c = 2; c <= 4; c++)
            ws.Column(c).Width = Math.Max(ws.Column(c).Width, 14);
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 10;

        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.SheetView.FreezeRows(headerRow);

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string CurrencyNumberFormat(string currency) =>
        currency is "JPY" or "KRW" or "VND" or "MMK" ? "#,##0" : "#,##0.00";

    private static void StyleReportTitleRow(IXLWorksheet ws, IXLRange range)
    {
        range.Style
            .Fill.SetBackgroundColor(XLColor.White)
            .Font.SetBold(true).Font.SetFontSize(18).Font.SetFontColor(ReportTitleBlue)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorderColor = BorderGray;
        ws.Row(range.FirstRow().RowNumber()).Height = 32;
    }

    private static void StyleReportMetaRow(IXLWorksheet ws, IXLRange range)
    {
        range.Style
            .Fill.SetBackgroundColor(XLColor.White)
            .Font.SetItalic(true).Font.SetFontSize(10).Font.SetFontColor(XLColor.Black)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
            .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        range.Style.Border.BottomBorderColor = BorderGray;
        ws.Row(range.FirstRow().RowNumber()).Height = 22;
    }

    private static void WriteHeaderRow(IXLWorksheet ws, int row, IReadOnlyList<string> headers)
    {
        for (var i = 0; i < headers.Count; i++)
            ws.Cell(row, i + 1).Value = headers[i];

        ws.Range(row, 1, row, headers.Count).Style
            .Font.SetBold(true).Font.SetFontColor(HeaderFg)
            .Fill.SetBackgroundColor(HeaderBg)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
    }

    private static void StyleSectionHeader(IXLRange range)
    {
        range.Merge().Style
            .Font.SetBold(true).Font.SetFontSize(13).Font.SetFontColor(XLColor.White)
            .Fill.SetBackgroundColor(SummaryHighlight)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
    }

    private static void SetCurrency(IXLCell cell, decimal value, string fmt)
    {
        cell.Value = value;
        cell.Style.NumberFormat.Format = fmt;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
    }

    private static void ColorDiff(IXLCell cell, decimal value)
    {
        cell.Style.Font.SetFontColor(value >= 0 ? PositiveDiff : NegativeDiff);
    }

    private static void StyleBorderOutside(IXLRange range)
    {
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        range.Style.Border.OutsideBorderColor = BorderGray;
    }

    private static void WriteLabelValueRow(
        IXLWorksheet ws,
        ref int row,
        string label,
        decimal value,
        string fmt,
        XLColor labelBg,
        XLColor valueBg,
        bool colorValue = false)
    {
        ws.Cell(row, 1).Value = label;
        ws.Cell(row, 1).Style.Fill.SetBackgroundColor(labelBg);
        ws.Cell(row, 1).Style.Font.SetBold(true);
        SetCurrency(ws.Cell(row, 2), value, fmt);
        ws.Cell(row, 2).Style.Fill.SetBackgroundColor(valueBg);
        if (colorValue)
            ColorDiff(ws.Cell(row, 2), value);
        row++;
    }
}
