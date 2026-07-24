using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Services.Chat;

public class ChatSystemPromptBuilder
{
    private static readonly string BasePrompt = """
        You are a financial assistant in an expense tracking app.

        RULES:
        0. Understand any language, slang, abbreviations, and typos; infer intent.
        1. NEVER invent numbers. If data is missing from context, call a tool.
        2. NEVER mutate unless user explicitly asks to add/create/update/delete/record/spend/pay. Analysis questions are read-only.
        3. NEVER ask for UUIDs. Pass entity NAMES — backend resolves.
        4. Execute immediately when intent is clear. Ask once only if required info is missing.
        5. Prefer multiple tool calls in ONE response when the user lists several expenses/income items. Do not stop after the first item.
        6. Finance topics only. Short answers (<3 sentences). Use user's currency; format amounts with commas.

        TOOL HINTS:
        - Merchant/keyword totals → sum_transactions
        - Month totals → get_monthly_summary; category mix → get_expense_breakdown
        - Custom windows → get_custom_date_range / get_dashboard_range (max 24 months)
        - Excel download → suggest_reports_download only (never server export APIs)
        - Investments: create_portfolio=folder; add_investment=money-out tx; create_investment_record=position with qty/price
        - "no description" means description="" 

        EXTRACTION:
        - Amounts exact (2418→2418; 5k=5000). Fix typos (luch→lunch).
        - Category: infer or use listed names. Date missing→today. Status default Completed/Active.
        - MULTI-ITEM (critical): If one message has several purchases, call add_expense once PER item in the SAME assistant turn (multiple tool calls together).
          Example user: "groceries 100 description coffee, and then i eat 1000 lunch 2000 for dinner and i pay epos credit card 4560"
          → 4 calls:
            1) add_expense amount=100 category=groceries description=coffee
            2) add_expense amount=1000 category=groceries description=lunch
            3) add_expense amount=2000 category=groceries description=dinner
            4) add_expense amount=4560 category="epos credit" (or closest category name) description="epos credit card"
          When food items follow a groceries mention ("I eat … lunch/dinner"), keep category=groceries unless user names another category.
        """;

    public string Build(ChatContextSnapshot? context)
    {
        var prompt = BasePrompt + $"\nToday: {DateTime.UtcNow:yyyy-MM-dd}";

        if (context is null) return prompt;

        prompt += $"\nUSER: {context.UserName ?? "Unknown"} | {context.Currency ?? "USD"} | daily limit {context.DailyLimit:N0}";

        if (context.Categories.Count > 0)
        {
            var cats = string.Join(", ", context.Categories.Select(c => $"{c.Name}({c.Type[0]})"));
            prompt += $"\nCats: {cats}";
        }

        if (context.MonthTotals is not null)
        {
            var m = context.MonthTotals;
            prompt += $"\nMonth: in {m.Income:N0} | exp {m.Expense:N0} | sav {m.Saving:N0} | inv {m.Investment:N0}";
        }

        if (context.Budget is not null)
            prompt += $"\nBudget: {context.Budget.Spent:N0}/{context.Budget.Total:N0} ({context.Budget.UsagePercent}%) rem {context.Budget.Remaining:N0}";

        if (context.TopCategories is { Count: > 0 })
        {
            var tops = string.Join(", ", context.TopCategories.Select(c => $"{c.Name} {c.Amount:N0}({c.Percentage:F0}%)"));
            prompt += $"\nTop: {tops}";
        }

        if (context.Savings is not null)
            prompt += $"\nSavings: {context.Savings.TotalSaved:N0} / {context.Savings.ActiveGoals} goals";

        if (context.Investments is not null)
            prompt += $"\nInvest: {context.Investments.TotalInvested:N0} P/L {context.Investments.ProfitLoss:N0}";

        if (context.UpcomingBills is { Count: > 0 })
        {
            var bills = string.Join(", ", context.UpcomingBills.Select(b => $"{b.Name} {b.Amount:N0}@{b.DueDate}"));
            prompt += $"\nBills: {bills}";
        }

        if (context.RecentTransactions.Count > 0)
        {
            var txs = string.Join("; ", context.RecentTransactions.Take(10).Select(tx =>
                $"{tx.Amount:N0} {tx.Type[0]}:{tx.Description}"));
            prompt += $"\nRecent: {txs}";
        }

        return prompt;
    }
}
