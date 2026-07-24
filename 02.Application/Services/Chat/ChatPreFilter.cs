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
            • **Recurring Payments** — list, create, update, delete, mark paid, acknowledge paid (clear missed)
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
    private static readonly Regex StructuredCategoryDescriptionPattern = new(
        @"^\s*category(?:\s+name)?\s+is\s+(?<category>.+?)\s+description\s+is\s+(?<description>.+?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ListPattern = new(
        @"^(show|list|my)\s+(my\s+)?(expenses?|incomes?|transactions?|categories|budget|recurring|bills?|savings?|investments?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SummaryPattern = new(
        @"^(summary|monthly\s*summary|this\s*month(\s*summary)?|dashboard|how\s+much\s+(did\s+i\s+)?spend(t)?(\s+this\s+month)?|total\s+spent(\s+this\s+month)?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BreakdownPattern = new(
        @"^(biggest\s+categor(y|ies)|top\s+categor(y|ies)|expense\s+breakdown|breakdown|where\s+(did\s+)?(my\s+)?money\s+go|spending\s+by\s+categor(y|ies))$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BudgetPattern = new(
        @"^(show\s+(my\s+)?budget|my\s+budget|get\s+budget|budget\s+status)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BillsPattern = new(
        @"^(upcoming\s+bills?|show\s+(my\s+)?(bills?|recurring)|my\s+(bills?|recurring))$",
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

        var multiItems = TryParseMultiExpenseItems(trimmed);
        if (multiItems is not null)
            return multiItems;

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
            var target = listMatch.Groups[3].Value.ToLowerInvariant().TrimEnd('s');
            var functionName = target switch
            {
                "expense" or "income" or "transaction" => "list_transactions",
                "categorie" or "category" => "list_categories",
                "budget" => "get_budget",
                "recurring" or "bill" => "list_recurring_payments",
                "saving" => "list_saving_goals",
                "investment" => "list_investments",
                _ => null
            };
            if (functionName is not null)
                return new DirectCommandMatch(functionName);
        }

        if (BudgetPattern.IsMatch(trimmed))
            return new DirectCommandMatch("get_budget");

        if (BillsPattern.IsMatch(trimmed))
            return new DirectCommandMatch("list_recurring_payments");

        if (BreakdownPattern.IsMatch(trimmed))
            return new DirectCommandMatch("get_expense_breakdown");

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
    private static readonly Regex CategoryAmountDescriptionPattern = new(
        @"^(?:category(?:\s+name)?\s+is\s+|category\s+)?(?<category>[A-Za-z][A-Za-z0-9 &\-]{0,40}?)\s+(?:amount\s+)?(?<amount>\d[\d,.]*k?)\s+(?:description(?:\s+is)?\s+)?(?<description>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex CategoryAmountOnlyPattern = new(
        @"^(?:pay\s+)?(?<category>[A-Za-z][A-Za-z0-9 &\-]{0,40}?)\s+(?<amount>\d[\d,.]*k?)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex EatMealPattern = new(
        @"(?:i\s+)?(?:eat|ate|had)\s+(?<a1>\d[\d,.]*k?)\s+(?<d1>lunch|luch|dinner|breakfast)\s+(?<a2>\d[\d,.]*k?)\s+(?:for\s+)?(?<d2>lunch|luch|dinner|breakfast)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PayBillPattern = new(
        @"(?:i\s+)?pay\s+(?<category>.+?)\s+(?<amount>\d[\d,.]*k?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses multi-item natural language like:
    /// "groceries 100 description coffee, and then i eat 1000 lunch 2000 for dinner and i pay epos credit card 4560"
    /// </summary>
    private static DirectCommandMatch? TryParseMultiExpenseItems(string trimmed)
    {
        var lines = new List<ExpenseLineDraft>();
        var working = trimmed;
        string? groceriesContext = null;

        // 1) Explicit "category amount description ..." first segment(s)
        var first = Regex.Match(working,
            @"^(?<category>[A-Za-z][A-Za-z0-9 &\-]{0,40}?)\s+(?<amount>\d[\d,.]*k?)\s+description(?:\s+is)?\s+(?<description>[^,]+)",
            RegexOptions.IgnoreCase);
        if (first.Success)
        {
            var amt = ParseAmount(first.Groups["amount"].Value);
            var cat = first.Groups["category"].Value.Trim();
            var desc = NormalizeDesc(first.Groups["description"].Value);
            if (amt > 0)
            {
                lines.Add(new ExpenseLineDraft(amt, cat, desc));
                if (cat.Contains("grocer", StringComparison.OrdinalIgnoreCase))
                    groceriesContext = cat;
            }
            working = working[first.Length..].Trim().TrimStart(',', ' ', '.');
            working = Regex.Replace(working, @"^(and\s+then\s+|and\s+|then\s+)", "", RegexOptions.IgnoreCase).Trim();
        }

        // 2) "i eat 1000 lunch 2000 for dinner"
        var eat = EatMealPattern.Match(working);
        if (eat.Success)
        {
            var cat = groceriesContext ?? "groceries";
            var a1 = ParseAmount(eat.Groups["a1"].Value);
            var a2 = ParseAmount(eat.Groups["a2"].Value);
            var d1 = NormalizeDesc(eat.Groups["d1"].Value);
            var d2 = NormalizeDesc(eat.Groups["d2"].Value);
            if (a1 > 0) lines.Add(new ExpenseLineDraft(a1, cat, d1));
            if (a2 > 0) lines.Add(new ExpenseLineDraft(a2, cat, d2));
            working = (working[..eat.Index] + working[(eat.Index + eat.Length)..]).Trim();
            working = Regex.Replace(working, @"^(and\s+then\s+|and\s+|then\s+|,)\s*", "", RegexOptions.IgnoreCase).Trim();
        }

        // 3) "i pay epos credit card 4560"
        var pay = PayBillPattern.Match(working);
        if (pay.Success)
        {
            var amt = ParseAmount(pay.Groups["amount"].Value);
            var cat = pay.Groups["category"].Value.Trim();
            // Prefer shorter category label for matching (drop trailing "card")
            var catForMatch = Regex.Replace(cat, @"\s+card$", "", RegexOptions.IgnoreCase).Trim();
            if (amt > 0)
                lines.Add(new ExpenseLineDraft(amt, catForMatch, cat));
            working = (working[..pay.Index] + working[(pay.Index + pay.Length)..]).Trim();
        }

        // 4) Remaining comma segments like "groceries 100 description coffee"
        foreach (var rawSeg in Regex.Split(working, @"\s*,\s*|\s+and\s+then\s+|\s+and\s+"))
        {
            var seg = rawSeg.Trim().TrimEnd('.');
            if (string.IsNullOrWhiteSpace(seg)) continue;

            var m1 = CategoryAmountDescriptionPattern.Match(seg);
            if (m1.Success)
            {
                var amt = ParseAmount(m1.Groups["amount"].Value);
                if (amt > 0)
                    lines.Add(new ExpenseLineDraft(amt, m1.Groups["category"].Value.Trim(), NormalizeDesc(m1.Groups["description"].Value)));
                continue;
            }

            var m2 = CategoryAmountOnlyPattern.Match(seg);
            if (m2.Success)
            {
                var amt = ParseAmount(m2.Groups["amount"].Value);
                var cat = m2.Groups["category"].Value.Trim();
                if (amt > 0 && !IsNoiseCategory(cat))
                    lines.Add(new ExpenseLineDraft(amt, cat, cat));
            }
        }

        // Need at least 2 distinct lines to treat as multi-item (otherwise let normal parsers handle)
        if (lines.Count < 2)
            return null;

        return new DirectCommandMatch("add_expense", Lines: lines);
    }

    private static bool IsNoiseCategory(string cat)
    {
        var c = cat.ToLowerInvariant();
        return c is "i" or "then" or "and" or "for" or "the" or "a" or "an" or "my" or "to" or "of";
    }

    private static string NormalizeDesc(string value)
    {
        var d = value.Trim().TrimEnd(',', '.');
        if (d.Equals("luch", StringComparison.OrdinalIgnoreCase)) return "lunch";
        return d;
    }

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

        var (parsedCategory, parsedDescription) = ParseStructuredCategoryAndDescription(description);
        return amounts.Count == 1
            ? new DirectCommandMatch(functionName, amounts[0], parsedDescription, parsedCategory)
            : new DirectCommandMatch(functionName, amounts[0], parsedDescription, parsedCategory, amounts.Skip(1).ToList());
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

    private static (string category, string description) ParseStructuredCategoryAndDescription(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (string.Empty, string.Empty);

        var match = StructuredCategoryDescriptionPattern.Match(text.Trim());
        if (!match.Success)
            return (text.Trim(), text.Trim());

        var category = match.Groups["category"].Value.Trim();
        var description = match.Groups["description"].Value.Trim();
        return (category, description);
    }
}

public record ExpenseLineDraft(decimal Amount, string Category, string Description);

public record DirectCommandMatch(
    string FunctionName,
    decimal Amount = 0,
    string Description = "",
    string Category = "",
    IReadOnlyList<decimal>? AdditionalAmounts = null,
    IReadOnlyList<ExpenseLineDraft>? Lines = null)
{
    /// <summary>Non-empty amounts for add_expense / add_income / add_savings direct commands.</summary>
    public IReadOnlyList<decimal> GetMoneyAmounts()
    {
        if (Lines is { Count: > 0 })
            return Array.Empty<decimal>(); // use Lines path instead

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
