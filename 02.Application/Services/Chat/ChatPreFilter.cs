using System.Text.RegularExpressions;
using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Services.Chat;

public class ChatPreFilter
{
    private readonly ChatHistoryStore _historyStore;

    private static readonly (Regex Pattern, string Response)[] QuickResponses =
    [
        (new Regex(@"^(hi|hello|hey|yo|sup)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Hello! I'm your financial assistant. How can I help you today?"),

        (new Regex(@"^(thanks?|thank\s*you|thx|ty)[\s!.]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "You're welcome! Let me know if you need anything else."),

        (new Regex(@"^(bye|goodbye|see\s*you|good\s*night)[\s!.]*$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            "Goodbye! Happy budgeting!"),

        (new Regex(@"^(help|what can you do|\?|commands)$", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            """
            Here's what I can help you with:
            • **Transactions** — add, list, update, delete expenses/income/investments/savings
            • **Categories** — list, create, update, delete categories
            • **Budgets** — view, create, update monthly budgets
            • **Recurring Payments** — list, create, update, delete, mark paid
            • **Saving Goals** — list, create, update, delete goals, add contributions
            • **Investments** — list, create, update, delete positions
            • **Reports** — monthly/yearly summaries, expense breakdowns, dashboard
            
            Try: "add expense 500 coffee" or "show my budget" or "monthly summary"
            """),
    ];

    private static readonly Regex OffTopicPattern = new(
        @"\b(weather|joke|tell me a|how to program|recipe|play a game|write (a |me )?code|sing|poem)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClearPattern = new(
        @"^(clear|reset|new\s*chat|start\s*over)[\s!.]*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Level 2: Direct command shortcut patterns
    private static readonly Regex AddExpensePattern = new(
        @"^add\s+expense\s+(\d[\d,.]*k?)\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AddIncomePattern = new(
        @"^add\s+income\s+(\d[\d,.]*k?)\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AddSavingsPattern = new(
        @"^add\s+savings?\s+(\d[\d,.]*k?)\s*(.*)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ListPattern = new(
        @"^(show|list)\s+(expenses?|incomes?|transactions?|categories|budget|recurring|savings?|investments?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SummaryPattern = new(
        @"^(summary|monthly\s*summary|dashboard)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ChatPreFilter(ChatHistoryStore historyStore)
    {
        _historyStore = historyStore;
    }

    public async Task<ChatResponse?> TryQuickResponseAsync(Guid userId, string message, string? userName)
    {
        var trimmed = message.Trim();

        // Clear/reset: wipe history and respond
        if (ClearPattern.IsMatch(trimmed))
        {
            await _historyStore.ClearHistoryAsync(userId);
            return new ChatResponse("Chat cleared! Fresh start. How can I help?", userName, null, null, DateTime.UtcNow);
        }

        // Quick conversational responses
        foreach (var (pattern, response) in QuickResponses)
        {
            if (pattern.IsMatch(trimmed))
                return new ChatResponse(response, userName, null, null, DateTime.UtcNow);
        }

        // Off-topic detection
        if (OffTopicPattern.IsMatch(trimmed))
            return new ChatResponse(
                "I focus on financial management. Try asking about expenses, budgets, savings, or investments!",
                userName, null, null, DateTime.UtcNow);

        return null;
    }

    public DirectCommandMatch? TryParseDirectCommand(string message)
    {
        var trimmed = message.Trim();

        var addExpense = AddExpensePattern.Match(trimmed);
        if (addExpense.Success)
            return new DirectCommandMatch("add_expense", ParseAmount(addExpense.Groups[1].Value), addExpense.Groups[2].Value.Trim());

        var addIncome = AddIncomePattern.Match(trimmed);
        if (addIncome.Success)
            return new DirectCommandMatch("add_income", ParseAmount(addIncome.Groups[1].Value), addIncome.Groups[2].Value.Trim());

        var addSavings = AddSavingsPattern.Match(trimmed);
        if (addSavings.Success)
            return new DirectCommandMatch("add_savings", ParseAmount(addSavings.Groups[1].Value), addSavings.Groups[2].Value.Trim());

        var listMatch = ListPattern.Match(trimmed);
        if (listMatch.Success)
        {
            var target = listMatch.Groups[2].Value.ToLowerInvariant().TrimEnd('s');
            var functionName = target switch
            {
                "expense" or "income" or "transaction" => "list_transactions",
                "categorie" or "category" => "list_categories",
                "budget" => "get_budget",
                "recurring" => "list_recurring_payments",
                "saving" => "list_saving_goals",
                "investment" => "list_investments",
                _ => null
            };
            if (functionName is not null)
                return new DirectCommandMatch(functionName);
        }

        var summaryMatch = SummaryPattern.Match(trimmed);
        if (summaryMatch.Success)
        {
            var fn = summaryMatch.Groups[0].Value.ToLowerInvariant().Contains("dashboard")
                ? "get_dashboard"
                : "get_monthly_summary";
            return new DirectCommandMatch(fn);
        }

        return null;
    }

    public static decimal ParseAmount(string raw)
    {
        var s = raw.Trim().Replace(",", "");
        if (s.EndsWith('k') || s.EndsWith('K'))
        {
            if (decimal.TryParse(s[..^1], out var kVal))
                return kVal * 1000;
        }
        return decimal.TryParse(s, out var val) ? val : 0;
    }
}

public record DirectCommandMatch(string FunctionName, decimal Amount = 0, string Description = "");
