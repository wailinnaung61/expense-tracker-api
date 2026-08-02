using System.Diagnostics;
using ClosedXML.Excel;
using expense_tracker_backend.Application.ExportLocal.Localization;
using expense_tracker_backend.Application.ExportLocal.Models;
using Microsoft.Extensions.Logging;

namespace expense_tracker_backend.Application.ExportLocal;

public static class ExcelReportBuilder
{
    // Colors
    private static readonly XLColor HeaderBg = XLColor.FromHtml("#1F4E79");
    private static readonly XLColor HeaderFg = XLColor.White;
    private static readonly XLColor SubHeaderBg = XLColor.FromHtml("#D6E4F0");
    private static readonly XLColor IncomeBg = XLColor.FromHtml("#E2EFDA");
    private static readonly XLColor ExpenseBg = XLColor.FromHtml("#FCE4EC");
    private static readonly XLColor TotalRowBg = XLColor.FromHtml("#FFF2CC");
    private static readonly XLColor SummaryHighlight = XLColor.FromHtml("#2E75B6");
    private static readonly XLColor PositiveDiff = XLColor.FromHtml("#548235");
    private static readonly XLColor NegativeDiff = XLColor.FromHtml("#C00000");

    public static byte[] Build(
        UserProfile user,
        List<TransactionRow> transactions,
        List<BudgetCategoryRow> budgetCategories,
        DateOnly startDate,
        DateOnly endDate,
        string locale = "en",
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        var sw = Stopwatch.StartNew();
        logger?.LogInformation($"[ExcelReportBuilder.Build] START — userId={user.UserId}, locale={locale}, startDate={startDate}, endDate={endDate}, transactions={transactions.Count}, budgetCategories={budgetCategories.Count}");

        try
        {
            var L = LocaleProvider.GetStrings(locale);
            using var workbook = new XLWorkbook();

            // Group transactions by month
            var months = new List<(DateOnly MonthStart, DateOnly MonthEnd, string Label)>();
            var current = new DateOnly(startDate.Year, startDate.Month, 1);
            var lastMonth = new DateOnly(endDate.Year, endDate.Month, 1);
            while (current <= lastMonth)
            {
                var monthEnd = current.AddMonths(1).AddDays(-1);
                months.Add((current, monthEnd, current.ToString("yyyy-MM")));
                current = current.AddMonths(1);
            }

            logger?.LogInformation($"[ExcelReportBuilder.Build] Months to generate: {months.Count} ({string.Join(", ", months.Select(m => m.Label))})");

            // Build Summary sheet first (will appear as first tab)
            logger?.LogInformation("[ExcelReportBuilder.Build] Building Summary sheet...");
            var summSw = Stopwatch.StartNew();
            BuildSummarySheet(workbook, user, transactions, months, startDate, endDate, L);
            logger?.LogInformation($"[ExcelReportBuilder.Build] Summary sheet built in {summSw.ElapsedMilliseconds}ms");

            // Build per-month sheets
            foreach (var (monthStart, monthEnd, label) in months)
            {
                logger?.LogInformation($"[ExcelReportBuilder.Build] Building month sheet: {label}...");
                var monthSw = Stopwatch.StartNew();

                var monthTx = transactions
                    .Where(t => t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd)
                    .ToList();

                var monthBudgets = budgetCategories
                    .Where(b => b.BudgetStart <= monthEnd && b.BudgetEnd >= monthStart)
                    .ToList();

                logger?.LogInformation($"[ExcelReportBuilder.Build] Month {label}: transactions={monthTx.Count}, budgets={monthBudgets.Count}");
                BuildMonthSheet(workbook, label, monthTx, monthBudgets, L);
                logger?.LogInformation($"[ExcelReportBuilder.Build] Month sheet {label} built in {monthSw.ElapsedMilliseconds}ms");
            }

            logger?.LogInformation($"[ExcelReportBuilder.Build] Saving workbook to memory stream...");
            var saveSw = Stopwatch.StartNew();
            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            var result = ms.ToArray();
            logger?.LogInformation($"[ExcelReportBuilder.Build] SUCCESS — totalSheets={workbook.Worksheets.Count}, fileSize={result.Length} bytes ({result.Length / 1024.0:F1} KB) | saveTime={saveSw.ElapsedMilliseconds}ms | totalElapsed={sw.ElapsedMilliseconds}ms");
            return result;
        }
        catch (Exception ex)
        {
            logger?.LogError($"[ExcelReportBuilder.Build] FAILED — userId={user.UserId}, locale={locale} | elapsed={sw.ElapsedMilliseconds}ms | error={ex.GetType().FullName}: {ex.Message} | stackTrace={ex.StackTrace}");
            if (ex.InnerException != null)
                logger?.LogError($"[ExcelReportBuilder.Build] InnerException: {ex.InnerException.GetType().FullName}: {ex.InnerException.Message} | stackTrace={ex.InnerException.StackTrace}");
            throw;
        }
    }

    // ───────────────────── SUMMARY SHEET ─────────────────────

    private static void BuildSummarySheet(
        XLWorkbook workbook,
        UserProfile user,
        List<TransactionRow> allTx,
        List<(DateOnly MonthStart, DateOnly MonthEnd, string Label)> months,
        DateOnly startDate,
        DateOnly endDate,
        ReportStrings L)
    {
        var ws = workbook.Worksheets.Add(L.Summary);
        var fmt = "#,##0.00";
        int row = 1;

        // ── Title
        ws.Cell(row, 1).Value = L.ReportTitle;
        ws.Range(row, 1, row, 6).Merge().Style
            .Font.SetBold(true).Font.SetFontSize(18).Font.SetFontColor(SummaryHighlight)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        row++;

        ws.Cell(row, 1).Value = $"{L.UserLabel}: {user.UserName}  |  {L.PeriodLabel}: {months.First().Label} ~ {months.Last().Label}  |  {L.CurrencyLabel}: {user.Currency}";
        ws.Range(row, 1, row, 6).Merge().Style
            .Font.SetFontSize(11).Font.SetItalic(true)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        row += 2;

        var completedTx = allTx.Where(t => t.PaymentStatus == "COMPLETED").ToList();

        // ── Section 1: Monthly Income vs Expense Overview
        ws.Cell(row, 1).Value = L.MonthlyOverview;
        StyleSectionHeader(ws.Range(row, 1, row, 6));
        row++;

        string[] overviewHeaders = [L.Month, L.Income, L.Expense, L.Investment, L.Savings, L.NetCashFlow];
        WriteHeaderRow(ws, row, overviewHeaders);
        row++;

        decimal grandIncome = 0, grandExpense = 0, grandInvest = 0, grandSavings = 0;
        int dataStartRow = row;

        foreach (var (monthStart, monthEnd, label) in months)
        {
            var mtx = completedTx.Where(t => t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd).ToList();
            decimal income = mtx.Where(t => t.Type == "INCOME").Sum(t => t.Amount);
            decimal expense = mtx.Where(t => t.Type == "EXPENSE").Sum(t => t.Amount);
            decimal invest = mtx.Where(t => t.Type == "INVESTMENT").Sum(t => t.Amount);
            decimal savings = mtx.Where(t => t.Type == "SAVINGS").Sum(t => t.Amount);
            decimal net = income - expense - invest - savings;

            ws.Cell(row, 1).Value = label;
            SetCurrency(ws.Cell(row, 2), income, fmt);
            SetCurrency(ws.Cell(row, 3), expense, fmt);
            SetCurrency(ws.Cell(row, 4), invest, fmt);
            SetCurrency(ws.Cell(row, 5), savings, fmt);
            SetCurrency(ws.Cell(row, 6), net, fmt);
            ColorDiff(ws.Cell(row, 6), net);

            if (row % 2 == 0) ws.Range(row, 1, row, 6).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F5F5F5"));

            grandIncome += income;
            grandExpense += expense;
            grandInvest += invest;
            grandSavings += savings;
            row++;
        }

        // Totals row
        ws.Cell(row, 1).Value = L.Total;
        ws.Cell(row, 1).Style.Font.SetBold(true);
        SetCurrency(ws.Cell(row, 2), grandIncome, fmt);
        SetCurrency(ws.Cell(row, 3), grandExpense, fmt);
        SetCurrency(ws.Cell(row, 4), grandInvest, fmt);
        SetCurrency(ws.Cell(row, 5), grandSavings, fmt);
        decimal grandNet = grandIncome - grandExpense - grandInvest - grandSavings;
        SetCurrency(ws.Cell(row, 6), grandNet, fmt);
        ColorDiff(ws.Cell(row, 6), grandNet);
        ws.Range(row, 1, row, 6).Style.Fill.SetBackgroundColor(TotalRowBg).Font.SetBold(true);
        StyleBorder(ws.Range(dataStartRow - 1, 1, row, 6));
        row += 2;

        // ── Section 2: Income Breakdown by Category
        ws.Cell(row, 1).Value = L.IncomeBreakdown;
        StyleSectionHeader(ws.Range(row, 1, row, 4));
        row++;

        WriteHeaderRow(ws, row, [L.Category, L.Amount, L.PercentOfTotalIncome, ""]);
        row++;

        var incomeByCategory = completedTx
            .Where(t => t.Type == "INCOME")
            .GroupBy(t => t.CategoryName)
            .Select(g => new { Category = g.Key, Amount = g.Sum(x => x.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToList();

        int incStart = row;
        foreach (var ic in incomeByCategory)
        {
            ws.Cell(row, 1).Value = ic.Category;
            SetCurrency(ws.Cell(row, 2), ic.Amount, fmt);
            decimal pct = grandIncome > 0 ? (ic.Amount / grandIncome) * 100 : 0;
            ws.Cell(row, 3).Value = pct;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0\"%\"";
            ws.Range(row, 1, row, 3).Style.Fill.SetBackgroundColor(IncomeBg);
            row++;
        }

        ws.Cell(row, 1).Value = L.TotalMonthlyIncome;
        SetCurrency(ws.Cell(row, 2), grandIncome, fmt);
        ws.Range(row, 1, row, 3).Style.Fill.SetBackgroundColor(TotalRowBg).Font.SetBold(true);
        if (incStart <= row) StyleBorder(ws.Range(incStart - 1, 1, row, 3));
        row += 2;

        // ── Section 3: Expense Breakdown by Category
        ws.Cell(row, 1).Value = L.ExpenseBreakdown;
        StyleSectionHeader(ws.Range(row, 1, row, 4));
        row++;

        WriteHeaderRow(ws, row, [L.Category, L.Amount, L.PercentOfTotalExpense, ""]);
        row++;

        var expenseByCategory = completedTx
            .Where(t => t.Type == "EXPENSE")
            .GroupBy(t => t.CategoryName)
            .Select(g => new { Category = g.Key, Amount = g.Sum(x => x.Amount) })
            .OrderByDescending(x => x.Amount)
            .ToList();

        int expStart = row;
        foreach (var ec in expenseByCategory)
        {
            ws.Cell(row, 1).Value = ec.Category;
            SetCurrency(ws.Cell(row, 2), ec.Amount, fmt);
            decimal pct = grandExpense > 0 ? (ec.Amount / grandExpense) * 100 : 0;
            ws.Cell(row, 3).Value = pct;
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.0\"%\"";
            ws.Range(row, 1, row, 3).Style.Fill.SetBackgroundColor(ExpenseBg);
            row++;
        }

        ws.Cell(row, 1).Value = L.TotalExpense;
        SetCurrency(ws.Cell(row, 2), grandExpense, fmt);
        ws.Range(row, 1, row, 3).Style.Fill.SetBackgroundColor(TotalRowBg).Font.SetBold(true);
        if (expStart <= row) StyleBorder(ws.Range(expStart - 1, 1, row, 3));
        row += 2;

        // ── Section 4: Transaction Status Summary
        ws.Cell(row, 1).Value = L.TransactionStatusSummary;
        StyleSectionHeader(ws.Range(row, 1, row, 4));
        row++;

        WriteHeaderRow(ws, row, [L.Status, L.Count, L.TotalAmount, ""]);
        row++;

        int statusStart = row;
        var statusMap = new Dictionary<string, string>
        {
            ["COMPLETED"] = L.Completed,
            ["PENDING"] = L.Pending,
            ["FAILED"] = L.Failed
        };

        foreach (var status in new[] { "COMPLETED", "PENDING", "FAILED" })
        {
            var group = allTx.Where(t => t.PaymentStatus == status).ToList();
            ws.Cell(row, 1).Value = statusMap[status];
            ws.Cell(row, 2).Value = group.Count;
            SetCurrency(ws.Cell(row, 3), group.Sum(t => t.Amount), fmt);
            row++;
        }

        ws.Cell(row, 1).Value = L.Total;
        ws.Cell(row, 2).Value = allTx.Count;
        SetCurrency(ws.Cell(row, 3), allTx.Sum(t => t.Amount), fmt);
        ws.Range(row, 1, row, 3).Style.Fill.SetBackgroundColor(TotalRowBg).Font.SetBold(true);
        if (statusStart <= row) StyleBorder(ws.Range(statusStart - 1, 1, row, 3));
        row += 2;

        // ── Section 5: Balance Summary box
        ws.Cell(row, 1).Value = L.BalanceSummary;
        StyleSectionHeader(ws.Range(row, 1, row, 4));
        row++;

        void SummaryLine(string label, decimal value, bool bold = false)
        {
            ws.Cell(row, 1).Value = label;
            SetCurrency(ws.Cell(row, 2), value, fmt);
            if (bold) ws.Range(row, 1, row, 2).Style.Font.SetBold(true);
            ColorDiff(ws.Cell(row, 2), value);
            row++;
        }

        SummaryLine(L.TotalIncomeCompleted, grandIncome);
        SummaryLine(L.TotalExpensesCompleted, grandExpense);
        SummaryLine(L.TotalInvestmentsCompleted, grandInvest);
        SummaryLine(L.TotalSavingsCompleted, grandSavings);
        SummaryLine(L.NetCashFlow, grandNet, true);
        row += 1;

        // ── Section 6: Averages & Projections
        int totalDays = endDate.DayNumber - startDate.DayNumber + 1;
        int totalMonths = months.Count;
        if (totalDays < 1) totalDays = 1;
        if (totalMonths < 1) totalMonths = 1;

        decimal avgDailyExpense = grandExpense / totalDays;
        decimal avgWeeklyExpense = avgDailyExpense * 7;
        decimal avgMonthlyExpense = grandExpense / totalMonths;
        decimal yearlyExpense = avgMonthlyExpense * 12;

        decimal avgDailyIncome = grandIncome / totalDays;
        decimal avgMonthlyIncome = grandIncome / totalMonths;
        decimal yearlyIncome = avgMonthlyIncome * 12;
        decimal yearlyNetSavings = yearlyIncome - yearlyExpense;

        ws.Cell(row, 1).Value = L.AveragesAndProjections;
        StyleSectionHeader(ws.Range(row, 1, row, 4));
        row++;

        WriteHeaderRow(ws, row, [L.Metric, L.Value, "", ""]);
        row++;

        int projStart = row;

        void ProjectionLine(string label, decimal value)
        {
            ws.Cell(row, 1).Value = label;
            SetCurrency(ws.Cell(row, 2), value, fmt);
            if (row % 2 == 0) ws.Range(row, 1, row, 2).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F5F5F5"));
            row++;
        }

        ProjectionLine(L.AvgDailySpending, avgDailyExpense);
        ProjectionLine(L.AvgWeeklySpending, avgWeeklyExpense);
        ProjectionLine(L.AvgMonthlySpending, avgMonthlyExpense);
        ProjectionLine(L.YearlyProjectionExpense, yearlyExpense);

        // Separator — income
        ProjectionLine(L.AvgDailyIncome, avgDailyIncome);
        ProjectionLine(L.AvgMonthlyIncome, avgMonthlyIncome);
        ProjectionLine(L.YearlyProjectionIncome, yearlyIncome);

        // Yearly net savings highlight
        ws.Cell(row, 1).Value = L.ProjectedYearlySavings;
        SetCurrency(ws.Cell(row, 2), yearlyNetSavings, fmt);
        ColorDiff(ws.Cell(row, 2), yearlyNetSavings);
        ws.Range(row, 1, row, 2).Style.Font.SetBold(true).Fill.SetBackgroundColor(TotalRowBg);
        StyleBorder(ws.Range(projStart - 1, 1, row, 2));

        // Auto-fit
        ws.Columns(1, 8).AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    // ───────────────────── MONTHLY SHEET ─────────────────────

    private static void BuildMonthSheet(
        XLWorkbook workbook,
        string monthLabel,
        List<TransactionRow> monthTx,
        List<BudgetCategoryRow> monthBudgets,
        ReportStrings L)
    {
        var ws = workbook.Worksheets.Add(monthLabel);
        var fmt = "#,##0.00";
        int row = 1;

        // Title
        ws.Cell(row, 1).Value = $"{L.Transactions} — {monthLabel}";
        ws.Range(row, 1, row, 8).Merge().Style
            .Font.SetBold(true).Font.SetFontSize(16).Font.SetFontColor(SummaryHighlight)
            .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center);
        row += 2;

        // Type display names
        var typeDisplayMap = new Dictionary<string, string>
        {
            ["INCOME"] = L.Income,
            ["EXPENSE"] = L.Expense,
            ["INVESTMENT"] = L.Investment,
            ["SAVINGS"] = L.Savings
        };

        // ── Budget vs Actual for this month
        if (monthBudgets.Count > 0)
        {
            ws.Cell(row, 1).Value = L.BudgetVsActual;
            StyleSectionHeader(ws.Range(row, 1, row, 6));
            row++;

            WriteHeaderRow(ws, row, [L.Category, L.Budgeted, L.ActualSpent, L.Difference, L.PercentUsed, ""]);
            row++;

            var completedExpenses = monthTx.Where(t => t.Type == "EXPENSE" && t.PaymentStatus == "COMPLETED").ToList();
            decimal mBudgetTotal = 0, mActualTotal = 0;
            int bStart = row;

            foreach (var bc in monthBudgets.OrderBy(b => b.CategoryName))
            {
                decimal actual = completedExpenses.Where(t => t.CategoryId == bc.CategoryId).Sum(t => t.Amount);
                decimal diff = bc.AllocatedAmount - actual;
                decimal pct = bc.AllocatedAmount > 0 ? (actual / bc.AllocatedAmount) * 100 : 0;

                ws.Cell(row, 1).Value = bc.CategoryName;
                SetCurrency(ws.Cell(row, 2), bc.AllocatedAmount, fmt);
                SetCurrency(ws.Cell(row, 3), actual, fmt);
                SetCurrency(ws.Cell(row, 4), diff, fmt);
                ColorDiff(ws.Cell(row, 4), diff);
                ws.Cell(row, 5).Value = pct;
                ws.Cell(row, 5).Style.NumberFormat.Format = "0.0\"%\"";
                if (pct > 100) ws.Cell(row, 5).Style.Font.SetFontColor(NegativeDiff);

                mBudgetTotal += bc.AllocatedAmount;
                mActualTotal += actual;
                row++;
            }

            ws.Cell(row, 1).Value = L.Subtotal;
            SetCurrency(ws.Cell(row, 2), mBudgetTotal, fmt);
            SetCurrency(ws.Cell(row, 3), mActualTotal, fmt);
            SetCurrency(ws.Cell(row, 4), mBudgetTotal - mActualTotal, fmt);
            ColorDiff(ws.Cell(row, 4), mBudgetTotal - mActualTotal);
            ws.Range(row, 1, row, 5).Style.Fill.SetBackgroundColor(TotalRowBg).Font.SetBold(true);
            StyleBorder(ws.Range(bStart - 1, 1, row, 5));
            row += 2;
        }

        // ── Income / Expense / Investment / Savings summary for this month
        var completed = monthTx.Where(t => t.PaymentStatus == "COMPLETED").ToList();
        decimal monthIncome = completed.Where(t => t.Type == "INCOME").Sum(t => t.Amount);
        decimal monthExpense = completed.Where(t => t.Type == "EXPENSE").Sum(t => t.Amount);
        decimal monthInvest = completed.Where(t => t.Type == "INVESTMENT").Sum(t => t.Amount);
        decimal monthSavings = completed.Where(t => t.Type == "SAVINGS").Sum(t => t.Amount);

        ws.Cell(row, 1).Value = L.MonthlySummary;
        StyleSectionHeader(ws.Range(row, 1, row, 3));
        row++;

        void AddSummaryRow(string label, decimal val, XLColor? bg = null)
        {
            ws.Cell(row, 1).Value = label;
            SetCurrency(ws.Cell(row, 2), val, fmt);
            if (bg != null) ws.Range(row, 1, row, 2).Style.Fill.SetBackgroundColor(bg);
            row++;
        }

        AddSummaryRow(L.Income, monthIncome, IncomeBg);
        AddSummaryRow(L.Expense, monthExpense, ExpenseBg);
        AddSummaryRow(L.Investment, monthInvest);
        AddSummaryRow(L.Savings, monthSavings);
        ws.Cell(row, 1).Value = L.NetCashFlow;
        decimal monthNet = monthIncome - monthExpense - monthInvest - monthSavings;
        SetCurrency(ws.Cell(row, 2), monthNet, fmt);
        ColorDiff(ws.Cell(row, 2), monthNet);
        ws.Range(row, 1, row, 2).Style.Font.SetBold(true).Fill.SetBackgroundColor(TotalRowBg);
        row += 2;

        // ── Detailed Transaction List (grouped by type)
        // Status display map
        var statusDisplayMap = new Dictionary<string, string>
        {
            ["COMPLETED"] = L.Completed,
            ["PENDING"] = L.Pending,
            ["FAILED"] = L.Failed
        };

        foreach (var type in new[] { "INCOME", "EXPENSE", "INVESTMENT", "SAVINGS" })
        {
            var typeTx = monthTx.Where(t => t.Type == type).ToList();
            if (typeTx.Count == 0) continue;

            var typeDisplay = typeDisplayMap.GetValueOrDefault(type, type);
            ws.Cell(row, 1).Value = string.Format(L.TransactionsLabel, typeDisplay);
            var typeBg = type == "INCOME" ? IncomeBg : type == "EXPENSE" ? ExpenseBg : SubHeaderBg;
            ws.Range(row, 1, row, 8).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(12)
                .Fill.SetBackgroundColor(typeBg);
            row++;

            string[] headers = [L.Date, L.Category, L.Description, L.Amount, L.Status, L.Notes, "", ""];
            WriteHeaderRow(ws, row, headers);
            row++;

            int detailStart = row;
            foreach (var tx in typeTx.OrderBy(t => t.TransactionDate))
            {
                ws.Cell(row, 1).Value = tx.TransactionDate.ToString("yyyy-MM-dd");
                ws.Cell(row, 2).Value = tx.CategoryName;
                ws.Cell(row, 3).Value = tx.Description;
                SetCurrency(ws.Cell(row, 4), tx.Amount, fmt);
                ws.Cell(row, 5).Value = statusDisplayMap.GetValueOrDefault(tx.PaymentStatus, tx.PaymentStatus);

                // Color-code status
                if (tx.PaymentStatus == "FAILED")
                    ws.Cell(row, 5).Style.Font.SetFontColor(NegativeDiff);
                else if (tx.PaymentStatus == "PENDING")
                    ws.Cell(row, 5).Style.Font.SetFontColor(XLColor.FromHtml("#BF8F00"));

                ws.Cell(row, 6).Value = tx.Notes;

                if (row % 2 == 0)
                    ws.Range(row, 1, row, 6).Style.Fill.SetBackgroundColor(XLColor.FromHtml("#F9F9F9"));
                row++;
            }

            // Subtotal
            ws.Cell(row, 1).Value = string.Format(L.SubtotalWithCount, typeTx.Count);
            ws.Range(row, 1, row, 3).Merge().Style.Font.SetBold(true);
            SetCurrency(ws.Cell(row, 4), typeTx.Sum(t => t.Amount), fmt);
            ws.Range(row, 1, row, 6).Style.Fill.SetBackgroundColor(TotalRowBg).Font.SetBold(true);
            StyleBorder(ws.Range(detailStart - 1, 1, row, 6));
            row += 2;
        }

        // Auto-fit
        ws.Columns(1, 8).AdjustToContents();
        ws.SheetView.FreezeRows(1);
    }

    // ───────────────────── Helpers ─────────────────────

    private static void WriteHeaderRow(IXLWorksheet ws, int row, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            if (string.IsNullOrEmpty(headers[i])) continue;
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
        }

        ws.Range(row, 1, row, headers.Length).Style
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
    }

    private static void ColorDiff(IXLCell cell, decimal value)
    {
        cell.Style.Font.SetFontColor(value >= 0 ? PositiveDiff : NegativeDiff);
    }

    private static void StyleBorder(IXLRange range)
    {
        range.Style.Border.SetOutsideBorder(XLBorderStyleValues.Thin)
            .Border.SetOutsideBorderColor(XLColor.FromHtml("#999999"));
    }
}
