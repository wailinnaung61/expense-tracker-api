using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace expense_tracker_backend.Application.Services;

public class ChatService : IChatService
{
    private readonly ITranactionService _transactionService;
    private readonly IExpenseCategoryService _categoryService;
    private readonly IAggregationService _aggregationService;
    private readonly IRecurringPaymentService _recurringPaymentService;
    private readonly IMemberRepository _memberRepository;
    private readonly ILogger<ChatService> _logger;
    private readonly IMemoryCache _memoryCache;
    private readonly ChatClient _chatClient;

    private const int MaxHistoryMessages = 20;
    private const int UserProfileCacheMinutes = 10;

    private static readonly string SystemPrompt = """
        You are a financial assistant embedded in an expense tracking application.

        PRIMARY OBJECTIVE:
        Help users accurately manage financial data (expenses, income, budgets, summaries, recurring payments, categories, investments).

        CORE RULES:
        1. Only respond to finance-related requests.
        2. If unrelated:
           - Do NOT call any function
           - Politely redirect to finance features
        3. Only call a function when intent is clear and actionable
        4. If unclear → ask a clarification question
        5. DELETE RULE:
           Step 1 — When user asks to delete, ALWAYS call find_transaction first to search by amount/description/date/type.
                    Show the matching records and ask "Do you want to delete this? Reply Yes to confirm."
           Step 2 — When user confirms (Yes/Confirm), call delete_transaction with the transaction_id from the previous result.
                    Delete immediately without asking again.
           Never ask the user to provide a transaction ID manually.
        6. Do NOT guess missing required data

        DATA EXTRACTION:
        - Amount:
          Extract numeric value only
          Support: 500, $500, 500 yen, 5k, 5.5k (k = thousand)

        - Category:
          Infer if missing:
          food/coffee → Food & Dining
          rent → Housing
          taxi/train → Transportation

        - Type:
          Expense → spending
          Income → salary/freelance
          Investment → stocks/crypto
          Savings → deposit

        - Date:
          "today" or missing → current date
          Format: YYYY-MM-DD

        - Status:
          Default = Completed
          If mentioned → Pending

        BEHAVIOR:
        - One request → one function
        - Multiple intents → ask user to clarify
        - If unsure → do NOT call function

        PERSONALIZATION:
        - Use user's currency
        - Use user's name if available (not excessive)

        RESPONSE STYLE:
        - Short, clear, professional
        - Minimal emojis

        EXAMPLES:

        User: "Add coffee 500 yen"
        → Call function add_expense

        User: "Salary 300000"
        → Call function add_income

        User: "Add 500"
        → Ask: "Is this an expense or income?"

        User: "Delete last expense"
        → Ask for confirmation

        User: "How are you?"
        → "I'm here to help manage your finances. You can track expenses or income anytime."

        FAIL-SAFE:
        If uncertain → ask, do not act
        """;

    public ChatService(
        ITranactionService transactionService,
        IExpenseCategoryService categoryService,
        IAggregationService aggregationService,
        IRecurringPaymentService recurringPaymentService,
        IMemberRepository memberRepository,
        ILogger<ChatService> logger,
        IMemoryCache memoryCache,
        IConfiguration configuration)
    {
        _transactionService = transactionService;
        _categoryService = categoryService;
        _aggregationService = aggregationService;
        _recurringPaymentService = recurringPaymentService;
        _memberRepository = memberRepository;
        _logger = logger;
        _memoryCache = memoryCache;

        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured");
        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        _chatClient = new ChatClient(model, apiKey);
    }

    public async Task<ChatResponse> ChatAsync(Guid userId, string message)
    {
        var tools = BuildToolDefinitions();

        // Keep first response fast: do not preload category lists for the prompt.
        // Category resolution happens only when a tool needs it.
        var profile = await GetUserProfileCachedAsync(userId);

        var userInfo = profile is not null
            ? $"\n\nUSER PROFILE:\n  - Name: {profile.UserName}\n  - Email: {profile.Email}\n  - Currency: {profile.Currency}\n  - Daily Limit: {profile.DailyLimit}\n  - Role: {profile.RoleId}\n  - Status: {profile.Status}"
            : "";

        var systemMessage = SystemPrompt
            + $"\nCurrent date: {DateTime.UtcNow:yyyy-MM-dd}"
            + userInfo
            + "\n\nIMPORTANT: If category_id is unknown, pass category name and backend will resolve the best matching category.";

        // Retrieve conversation history from cache (excludes system message)
        var cacheKey = $"chat:{userId}";
        var history = _memoryCache.TryGetValue(cacheKey, out List<ChatMessage>? cached)
            ? cached!
            : [];

        // Build messages: fresh system prompt + conversation history + new user message
        var messages = new List<ChatMessage>(history.Count + 2) { new SystemChatMessage(systemMessage) };
        messages.AddRange(history);
        messages.Add(new UserChatMessage(message));

        var options = new ChatCompletionOptions();
        foreach (var tool in tools)
            options.Tools.Add(tool);

        _logger.LogInformation("Sending chat request to OpenAI for user {UserId}: {Message} (history: {HistoryCount} msgs)",
            userId, message, history.Count);

        var response = await _chatClient.CompleteChatAsync(messages, options);
        var choice = response.Value;

        ChatResponse chatResponse;

        // If the model wants to call function(s) — could be 1 or many at once
        if (choice.FinishReason == ChatFinishReason.ToolCalls)
        {
            // Step 1: Add the assistant message with all tool_calls
            messages.Add(new AssistantChatMessage(choice));

            // Step 2: Execute ALL tool calls and add a ToolChatMessage for EACH one
            var functionsCalled = new List<FunctionCallResult>();

            foreach (var toolCall in choice.ToolCalls)
            {
                _logger.LogInformation("OpenAI function call: {FunctionName}, args: {Args}",
                    toolCall.FunctionName, toolCall.FunctionArguments);

                var (result, resultObj) = await ExecuteFunctionAsync(userId, toolCall.FunctionName, toolCall.FunctionArguments.ToString());

                // Each tool_call_id MUST have a matching ToolChatMessage response
                messages.Add(new ToolChatMessage(toolCall.Id, result));
                functionsCalled.Add(new FunctionCallResult(toolCall.FunctionName, resultObj));
            }

            // Step 3: Send all results back to OpenAI for a single natural language summary
            var followUp = await _chatClient.CompleteChatAsync(messages, options);
            var followUpChoice = followUp.Value;
            var followUpText = followUpChoice.Content.FirstOrDefault()?.Text
                ?? string.Join("\n", functionsCalled.Select(f => f.FunctionName));

            // Add the final assistant response for history continuity
            messages.Add(new AssistantChatMessage(followUpChoice));

            var refreshTarget = ResolveRefreshTarget(functionsCalled);
            chatResponse = new ChatResponse(followUpText, profile?.UserName, refreshTarget, functionsCalled, DateTime.UtcNow);
        }
        else
        {
            // Normal text response (no function call) — add to history
            var text = choice.Content.FirstOrDefault()?.Text ?? "I'm sorry, I couldn't process your request.";
            messages.Add(new AssistantChatMessage(choice));
            chatResponse = new ChatResponse(text, profile?.UserName, null, null, DateTime.UtcNow);
        }

        // Save conversation history (skip system message, cap at MaxHistoryMessages)
        var newHistory = messages.Skip(1).TakeLast(MaxHistoryMessages).ToList();
        _memoryCache.Set(cacheKey, newHistory, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(30)
        });

        return chatResponse;
    }

    private async Task<MemberProfile?> GetUserProfileCachedAsync(Guid userId)
    {
        var cacheKey = $"chat:profile:{userId}";
        if (_memoryCache.TryGetValue(cacheKey, out MemberProfile? cachedProfile))
            return cachedProfile;

        var profile = await _memberRepository.GetProfileByUserIdAsync(userId.ToString());
        _memoryCache.Set(cacheKey, profile, new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(UserProfileCacheMinutes)
        });

        return profile;
    }

    private async Task<(string Summary, object? Data)> ExecuteFunctionAsync(Guid userId, string functionName, string argsJson)
    {
        try
        {
            var args = JsonDocument.Parse(argsJson).RootElement;

            return functionName switch
            {
                "add_expense" => await AddTransactionAsync(userId, AppConstants.TransactionType.Expense, args),
                "add_income" => await AddTransactionAsync(userId, AppConstants.TransactionType.Income, args),
                "add_investment" => await AddTransactionAsync(userId, AppConstants.TransactionType.Investment, args),
                "add_savings" => await AddTransactionAsync(userId, AppConstants.TransactionType.Savings, args),
                "list_transactions" => await ListTransactionsAsync(userId, args),
                "get_monthly_summary" => await GetMonthlySummaryAsync(userId, args),
                "get_yearly_summary" => await GetYearlySummaryAsync(userId, args),
                "get_expense_breakdown" => await GetExpenseBreakdownAsync(userId, args),
                "list_categories" => await ListCategoriesAsync(userId, args),
                "list_recurring_payments" => await ListRecurringPaymentsAsync(userId),
                "find_transaction" => await FindTransactionAsync(userId, args),
                "delete_transaction" => await DeleteTransactionAsync(userId, args),
                "find_and_delete_transaction" => await FindAndDeleteTransactionAsync(userId, args),
                _ => ($"Unknown function: {functionName}", null)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return ($"Error executing {functionName}: {ex.Message}", null);
        }
    }

    private async Task<(string, object?)> AddTransactionAsync(Guid userId, AppConstants.TransactionType type, JsonElement args)
    {
        var amount = args.GetProperty("amount").GetDecimal();
        var description = args.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
        var categoryId = args.TryGetProperty("category_id", out var cat) ? cat.GetString() ?? "" : "";
        var date = args.TryGetProperty("date", out var d) ? d.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM-dd") : DateTime.UtcNow.ToString("yyyy-MM-dd");
        var statusStr = args.TryGetProperty("status", out var s) ? s.GetString() ?? "Completed" : "Completed";
        var status = statusStr.Equals("Pending", StringComparison.OrdinalIgnoreCase)
            ? AppConstants.PaymentStatus.Pending
            : AppConstants.PaymentStatus.Completed;

        // If no category_id provided, try to find one by name
        if (string.IsNullOrEmpty(categoryId) && args.TryGetProperty("category", out var catName))
        {
            var categoryName = catName.GetString();
            if (!string.IsNullOrEmpty(categoryName))
            {
                var categories = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest
                {
                    Type = type,
                    Keyword = categoryName,
                    PageSize = 1
                });
                if (categories.Items.Count > 0)
                    categoryId = categories.Items[0].CategoryId.ToString();
            }
        }

        // Fallback: get first category of matching type
        if (string.IsNullOrEmpty(categoryId))
        {
            var categories = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest
            {
                Type = type,
                PageSize = 1
            });
            if (categories.Items.Count > 0)
                categoryId = categories.Items[0].CategoryId.ToString();
            else
                return ("No categories found. Please create a category first.", null);
        }

        var dto = new CreateTranactionDto(type, categoryId, amount, date, status, description, "", "");
        var result = await _transactionService.CreateTranactionAsync(dto, userId);

        var summary = $"✅ Added {type} of {amount:N0} on {date}";
        if (!string.IsNullOrEmpty(description)) summary += $" — {description}";

        return (summary, result);
    }

    private async Task<(string, object?)> ListTransactionsAsync(Guid userId, JsonElement args)
    {
        AppConstants.TransactionType? type = null;
        if (args.TryGetProperty("type", out var t))
        {
            var typeStr = t.GetString();
            if (Enum.TryParse<AppConstants.TransactionType>(typeStr, true, out var parsed))
                type = parsed;
        }

        var pageSize = args.TryGetProperty("limit", out var l) ? l.GetInt32() : 10;

        var filter = new TransactionFilterRequest
        {
            Type = type,
            PageSize = Math.Clamp(pageSize, 1, 20)
        };

        var result = await _transactionService.GetTransactionsAsync(userId, filter);

        if (result.Items.Count == 0)
            return ("No transactions found.", result);

        var lines = result.Items.Select(tx =>
            $"• {tx.type} | {tx.Amount:N0} | {tx.TranactionDate} | {tx.Description}");
        var summary = $"📋 Found {result.TotalCount} transaction(s):\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    private async Task<(string, object?)> GetMonthlySummaryAsync(Guid userId, JsonElement args)
    {
        var month = args.TryGetProperty("month", out var m) ? m.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM") : DateTime.UtcNow.ToString("yyyy-MM");

        var result = await _aggregationService.GetMonthlyAggregationAsync(userId, month);

        if (result is null)
            return ($"No data found for {month}.", null);

        var summary = $"""
            📊 Monthly Summary for {month}:
            • Income: {result.Income:N0}
            • Expense: {result.Expense:N0}
            • Savings: {result.Saving:N0}
            • Investment: {result.Investment:N0}
            • Net: {result.Income - result.Expense - result.Saving - result.Investment:N0}
            • Transactions: {result.TransactionCount}
            """;

        return (summary, result);
    }

    private async Task<(string, object?)> GetYearlySummaryAsync(Guid userId, JsonElement args)
    {
        var year = args.TryGetProperty("year", out var y) ? y.GetString() ?? DateTime.UtcNow.ToString("yyyy") : DateTime.UtcNow.ToString("yyyy");

        var result = await _aggregationService.GetYearlyAggregationAsync(userId, year);

        if (result is null)
            return ($"No data found for {year}.", null);

        var summary = $"""
            📊 Yearly Summary for {year}:
            • Income: {result.Income:N0}
            • Expense: {result.Expense:N0}
            • Savings: {result.Saving:N0}
            • Investment: {result.Investment:N0}
            • Transactions: {result.TransactionCount}
            """;

        return (summary, result);
    }

    private async Task<(string, object?)> GetExpenseBreakdownAsync(Guid userId, JsonElement args)
    {
        var month = args.TryGetProperty("month", out var m) ? m.GetString() ?? DateTime.UtcNow.ToString("yyyy-MM") : DateTime.UtcNow.ToString("yyyy-MM");

        var result = await _aggregationService.GetExpenseBreakdownAsync(userId, month);

        if (result.Categories.Count == 0)
            return ($"No expense data found for {month}.", result);

        var lines = result.Categories.Select(c =>
            $"• {c.CategoryName}: {c.Amount:N0} ({c.Percentage:F1}%)");
        var summary = $"💰 Expense Breakdown for {month} (Total: {result.TotalExpenses:N0}):\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    private async Task<(string, object?)> ListCategoriesAsync(Guid userId, JsonElement args)
    {
        AppConstants.TransactionType? type = null;
        if (args.TryGetProperty("type", out var t))
        {
            var typeStr = t.GetString();
            if (Enum.TryParse<AppConstants.TransactionType>(typeStr, true, out var parsed))
                type = parsed;
        }

        var filter = new CategoryFilterRequest { Type = type, PageSize = 50 };
        var result = await _categoryService.GetCategoriesAsync(userId, filter);

        if (result.Items.Count == 0)
            return ("No categories found.", result);

        var lines = result.Items.Select(c => $"• {c.DisplayName} ({c.Type}) {c.Icon}");
        var summary = $"📂 Categories:\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    private async Task<(string, object?)> ListRecurringPaymentsAsync(Guid userId)
    {
        var result = await _recurringPaymentService.GetAllAsync(userId);

        if (result.Count == 0)
            return ("No recurring payments found.", result);

        var lines = result.Select(r =>
            $"• {r.Name}: {r.Amount:N0} ({r.Frequency}) — Next: {r.NextDueDate:yyyy-MM-dd} [{r.Status}]");
        var summary = $"🔁 Recurring Payments:\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    private async Task<(string, object?)> DeleteTransactionAsync(Guid userId, JsonElement args)
    {
        var idStr = args.TryGetProperty("transaction_id", out var id) ? id.GetString() : null;
        if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out var transactionId))
            return ("Please provide a valid transaction ID to delete.", null);

        var result = await _transactionService.DeleteTranactionAsync(userId, transactionId);
        return result
            ? ("🗑️ Transaction deleted successfully.", true)
            : ("Transaction not found.", false);
    }

    private async Task<(string, object?)> FindTransactionAsync(Guid userId, JsonElement args)
    {
        var keyword = args.TryGetProperty("description", out var desc) ? desc.GetString() : null;
        var amount = args.TryGetProperty("amount", out var amt) ? amt.GetDecimal() : (decimal?)null;
        var date = args.TryGetProperty("date", out var d) ? d.GetString() : null;

        AppConstants.TransactionType? type = null;
        if (args.TryGetProperty("type", out var t) && Enum.TryParse<AppConstants.TransactionType>(t.GetString(), true, out var parsedType))
            type = parsedType;

        DateTime? startDate = null;
        DateTime? endDate = null;
        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate))
        {
            startDate = parsedDate.Date;
            endDate = parsedDate.Date.AddDays(1).AddTicks(-1);
        }

        var filter = new TransactionFilterRequest
        {
            Keyword = keyword,
            Type = type,
            StartDate = startDate,
            EndDate = endDate,
            PageSize = 10
        };

        var result = await _transactionService.GetTransactionsAsync(userId, filter);

        var matches = amount.HasValue
            ? result.Items.Where(tx => tx.Amount == amount.Value).ToList()
            : result.Items;

        if (matches.Count == 0)
            return ("No matching transaction found. Please check the amount, description, or date.", null);

        var lines = matches.Select(tx =>
            $"• {tx.Amount:N0} | {tx.TranactionDate} | {tx.Description}");
        var summary = $"Found {matches.Count} matching transaction(s):\n{string.Join("\n", lines)}\n\nDo you want to delete this? Reply Yes to confirm.";

        return (summary, matches);
    }

    private async Task<(string, object?)> FindAndDeleteTransactionAsync(Guid userId, JsonElement args)
    {
        var keyword = args.TryGetProperty("description", out var desc) ? desc.GetString() : null;
        var amount = args.TryGetProperty("amount", out var amt) ? amt.GetDecimal() : (decimal?)null;
        var date = args.TryGetProperty("date", out var d) ? d.GetString() : null;

        AppConstants.TransactionType? type = null;
        if (args.TryGetProperty("type", out var t) && Enum.TryParse<AppConstants.TransactionType>(t.GetString(), true, out var parsedType))
            type = parsedType;

        // Build date range for the given date (whole day)
        DateTime? startDate = null;
        DateTime? endDate = null;
        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate))
        {
            startDate = parsedDate.Date;
            endDate = parsedDate.Date.AddDays(1).AddTicks(-1);
        }

        var filter = new TransactionFilterRequest
        {
            Keyword = keyword,
            Type = type,
            StartDate = startDate,
            EndDate = endDate,
            PageSize = 10
        };

        var result = await _transactionService.GetTransactionsAsync(userId, filter);

        // Filter by amount if provided
        var matches = amount.HasValue
            ? result.Items.Where(tx => tx.Amount == amount.Value).ToList()
            : result.Items;

        if (matches.Count == 0)
            return ("No matching transaction found. Please check the amount, description, or date.", null);

        if (matches.Count > 1)
        {
            var lines = matches.Select(tx =>
                $"• {tx.Amount:N0} | {tx.TranactionDate} | {tx.Description} | ID: {tx.TranactionId}");
            return ($"Found {matches.Count} matching transactions. Please specify which one:\n{string.Join("\n", lines)}", matches);
        }

        // Exactly one match — delete it
        var match = matches[0];
        var deleted = await _transactionService.DeleteTranactionAsync(userId, match.TranactionId);
        return deleted
            ? ($"🗑️ Deleted: {match.Amount:N0} — {match.Description} on {match.TranactionDate}", match)
            : ("Failed to delete transaction.", null);
    }

    // ============================================================================
    // HELPERS
    // ============================================================================

    private static string? ResolveRefreshTarget(IEnumerable<FunctionCallResult> functions)
    {
        var name = functions.FirstOrDefault()?.FunctionName;
        return name switch
        {
            // Mutating operations only — frontend should refresh after these
            "add_expense" or "add_income" or "add_investment" or "add_savings"
                or "delete_transaction" or "find_and_delete_transaction" => AppConstants.ChatRefreshTarget.Transactions,
            // Read operations return null — no refresh needed
            _ => null
        };
    }

    private static List<ChatTool> BuildToolDefinitions()
    {
        return
        [
            ChatTool.CreateFunctionTool(
                "add_expense",
                "Add a new expense transaction. Use when user wants to record spending money.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "amount": { "type": "number", "description": "The expense amount" },
                        "description": { "type": "string", "description": "What the expense is for" },
                        "category": { "type": "string", "description": "Category name like Food, Transport, Shopping" },
                        "category_id": { "type": "string", "description": "Category UUID if known" },
                        "date": { "type": "string", "description": "Date in yyyy-MM-dd format. Use today if not specified." },
                        "status": { "type": "string", "enum": ["Completed", "Pending"], "description": "Payment status, default Completed" }
                    },
                    "required": ["amount"]
                }
                """)),

            ChatTool.CreateFunctionTool(
                "add_income",
                "Add a new income transaction. Use when user mentions receiving money, salary, freelance payment.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "amount": { "type": "number", "description": "The income amount" },
                        "description": { "type": "string", "description": "Income source description" },
                        "category": { "type": "string", "description": "Category name like Salary, Freelance" },
                        "category_id": { "type": "string", "description": "Category UUID if known" },
                        "date": { "type": "string", "description": "Date in yyyy-MM-dd format" },
                        "status": { "type": "string", "enum": ["Completed", "Pending"] }
                    },
                    "required": ["amount"]
                }
                """)),

            ChatTool.CreateFunctionTool(
                "add_investment",
                "Add a new investment transaction. Use when user mentions stocks, crypto, funds.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "amount": { "type": "number", "description": "The investment amount" },
                        "description": { "type": "string", "description": "Investment description" },
                        "category": { "type": "string", "description": "Category name" },
                        "category_id": { "type": "string", "description": "Category UUID if known" },
                        "date": { "type": "string", "description": "Date in yyyy-MM-dd format" }
                    },
                    "required": ["amount"]
                }
                """)),

            ChatTool.CreateFunctionTool(
                "add_savings",
                "Add a new savings transaction. Use when user mentions saving, deposit to savings.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "amount": { "type": "number", "description": "The savings amount" },
                        "description": { "type": "string", "description": "Savings description" },
                        "category": { "type": "string", "description": "Category name" },
                        "category_id": { "type": "string", "description": "Category UUID if known" },
                        "date": { "type": "string", "description": "Date in yyyy-MM-dd format" }
                    },
                    "required": ["amount"]
                }
                """)),

            ChatTool.CreateFunctionTool(
                "list_transactions",
                "List recent transactions. Use when user asks to see or show expenses, income, or transactions.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "type": { "type": "string", "enum": ["Expense", "Income", "Investment", "Savings"], "description": "Filter by type" },
                        "limit": { "type": "integer", "description": "Number of transactions to show, default 10" }
                    }
                }
                """)),

            ChatTool.CreateFunctionTool(
                "get_monthly_summary",
                "Get monthly financial summary with income, expense, savings, investment totals.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "month": { "type": "string", "description": "Month in yyyy-MM format. Use current month if not specified." }
                    }
                }
                """)),

            ChatTool.CreateFunctionTool(
                "get_yearly_summary",
                "Get yearly financial summary.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "year": { "type": "string", "description": "Year in yyyy format. Use current year if not specified." }
                    }
                }
                """)),

            ChatTool.CreateFunctionTool(
                "get_expense_breakdown",
                "Get expense breakdown by category for a month. Shows how much was spent per category.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "month": { "type": "string", "description": "Month in yyyy-MM format" }
                    }
                }
                """)),

            ChatTool.CreateFunctionTool(
                "list_categories",
                "List available categories. Use when user asks about categories or what categories exist.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "type": { "type": "string", "enum": ["Expense", "Income", "Investment", "Savings"], "description": "Filter by type" }
                    }
                }
                """)),

            ChatTool.CreateFunctionTool(
                "list_recurring_payments",
                "List all recurring payments. Use when user asks about recurring bills, subscriptions, or scheduled payments.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {}
                }
                """)),

            ChatTool.CreateFunctionTool(
                "find_transaction",
                "Search for transactions by amount, description, date, or type. Use this as Step 1 before deleting — show results to user and ask for confirmation.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "amount": { "type": "number", "description": "The transaction amount to match" },
                        "description": { "type": "string", "description": "Keyword to search in description" },
                        "date": { "type": "string", "description": "Date in yyyy-MM-dd format. Use today if user says 'today'." },
                        "type": { "type": "string", "enum": ["Expense", "Income", "Investment", "Savings"], "description": "Transaction type filter" }
                    }
                }
                """)),

            ChatTool.CreateFunctionTool(
                "delete_transaction",
                "Delete a transaction by its exact ID. Use this as Step 2 after user confirms deletion.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "transaction_id": { "type": "string", "description": "The transaction UUID to delete" }
                    },
                    "required": ["transaction_id"]
                }
                """)),

            ChatTool.CreateFunctionTool(
                "find_and_delete_transaction",
                "Search for a transaction by amount, description, or date and delete it. Use this when the user wants to delete but does NOT provide a transaction ID.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "amount": { "type": "number", "description": "The transaction amount to match" },
                        "description": { "type": "string", "description": "Keyword to search in description" },
                        "date": { "type": "string", "description": "Date in yyyy-MM-dd format. Use today if user says 'today'." },
                        "type": { "type": "string", "enum": ["Expense", "Income", "Investment", "Savings"], "description": "Transaction type filter" }
                    }
                }
                """))
        ];
    }
}
