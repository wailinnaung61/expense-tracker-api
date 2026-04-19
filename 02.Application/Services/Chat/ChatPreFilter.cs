using System.Linq;
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
            • **Excel export** — ask in chat; the app shows a **Reports → Download** button (not started from chat APIs)
            
            Try: "add expense 500 coffee", "add expense groceries 2418 and 1371", "show my budget", or "monthly summary"
            """),
    ];

    private static readonly Regex OffTopicPattern = new(
        @"\b(weather|joke|tell me a|how to program|recipe|play a game|write (a |me )?code|sing|poem)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ClearPattern = new(
        @"^(clear|reset|new\s*chat|start\s*over)[\s!.]*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex AddSavingsLeadIn = new(
        @"^add\s+savings?\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AndMoreAmountPrefix = new(
        @"^and\s+(\d[\d,.]*k?)\s*",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AmountToken = new(
        @"\b(\d[\d,.]*k?)\b",
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

        // Excel / report download → UI hint only (no export API from chat)
        if (ChatReportsDownloadIntent.Matches(trimmed))
        {
            var action = ChatReportsDownloadIntent.BuildClientActionFromMessage(trimmed);
            return new ChatResponse(ChatReportsDownloadIntent.AssistantMessage, userName, null, null, DateTime.UtcNow, action);
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

        var addExpense = TryParseFlexibleMoneyCommand(trimmed, "add expense", "add_expense");
        if (addExpense is not null)
            return addExpense;

        var addIncome = TryParseFlexibleMoneyCommand(trimmed, "add income", "add_income");
        if (addIncome is not null)
            return addIncome;

        var addSavings = TryParseFlexibleSavingsCommand(trimmed);
        if (addSavings is not null)
            return addSavings;

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

    /// <summary>
    /// Parses "add expense|income …" tails: amount-first ("500 coffee", "50 and 100 lunch"),
    /// or description-first with one or more amounts ("groceries 2418 and 1371").
    /// </summary>
    private static DirectCommandMatch? TryParseFlexibleMoneyCommand(string trimmed, string prefix, string functionName)
    {
        var lead = prefix + " ";
        if (!trimmed.StartsWith(lead, StringComparison.OrdinalIgnoreCase))
            return null;

        var tail = trimmed[lead.Length..].Trim();
        if (string.IsNullOrEmpty(tail))
            return null;

        var parsed = ParseMoneyCommandTail(tail);
        return parsed is null ? null : BuildMoneyDirectCommand(functionName, parsed.Value.amounts, parsed.Value.description);
    }

    private static DirectCommandMatch? TryParseFlexibleSavingsCommand(string trimmed)
    {
        var m = AddSavingsLeadIn.Match(trimmed);
        if (!m.Success)
            return null;

        var tail = trimmed[m.Length..].Trim();
        if (string.IsNullOrEmpty(tail))
            return null;

        var parsed = ParseMoneyCommandTail(tail);
        return parsed is null ? null : BuildMoneyDirectCommand("add_savings", parsed.Value.amounts, parsed.Value.description);
    }

    private static DirectCommandMatch? BuildMoneyDirectCommand(string functionName, List<decimal> amounts, string description)
    {
        if (amounts.Count == 0 || amounts[0] <= 0)
            return null;

        return amounts.Count == 1
            ? new DirectCommandMatch(functionName, amounts[0], description)
            : new DirectCommandMatch(functionName, amounts[0], description, amounts.Skip(1).ToList());
    }

    private static (List<decimal> amounts, string description)? ParseMoneyCommandTail(string tail)
    {
        var amountFirst = Regex.Match(tail, @"^(\d[\d,.]*k?)\s*(.*)$", RegexOptions.IgnoreCase);
        if (amountFirst.Success)
        {
            var first = ParseAmount(amountFirst.Groups[1].Value);
            if (first <= 0)
                return null;

            var amounts = new List<decimal> { first };
            var rest = amountFirst.Groups[2].Value.Trim();
            while (true)
            {
                var chain = AndMoreAmountPrefix.Match(rest);
                if (!chain.Success)
                    break;
                var next = ParseAmount(chain.Groups[1].Value);
                if (next <= 0)
                    break;
                amounts.Add(next);
                rest = rest[chain.Length..].Trim();
            }

            return (amounts, rest);
        }

        var parsedAmounts = new List<decimal>();
        foreach (Match m in AmountToken.Matches(tail))
        {
            var a = ParseAmount(m.Groups[1].Value);
            if (a > 0)
                parsedAmounts.Add(a);
        }

        if (parsedAmounts.Count == 0)
            return null;

        var firstNum = AmountToken.Match(tail);
        var desc = tail[..firstNum.Index].Trim();
        if (string.IsNullOrEmpty(desc))
            return null;

        return (parsedAmounts, desc);
    }
}

public record DirectCommandMatch(
    string FunctionName,
    decimal Amount = 0,
    string Description = "",
    IReadOnlyList<decimal>? AdditionalAmounts = null)
{
    /// <summary>Non-empty amounts for add_expense / add_income / add_savings direct commands.</summary>
    public IReadOnlyList<decimal> GetMoneyAmounts()
    {
        if (FunctionName is not ("add_expense" or "add_income" or "add_savings"))
            return Array.Empty<decimal>();
        if (Amount <= 0)
            return Array.Empty<decimal>();
        if (AdditionalAmounts is null || AdditionalAmounts.Count == 0)
            return new[] { Amount };

        var list = new List<decimal> { Amount };
        list.AddRange(AdditionalAmounts.Where(a => a > 0));
        return list;
    }
}
