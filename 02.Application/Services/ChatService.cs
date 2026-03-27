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

/// <summary>
/// AI Chat Service — sends user messages to OpenAI GPT with function calling.
/// 
/// Endpoint: POST /api/chat
/// Headers:  Authorization: Bearer {jwt_token}
/// 
/// ─────────────────────────────────────────────────────────────────
/// REQUEST SAMPLES (POST /api/chat)
/// ─────────────────────────────────────────────────────────────────
/// 
/// 1. Add expense:
///    { "message": "Add coffee 500 yen" }
///    → Calls: add_expense({ amount: 500, description: "coffee", category: "Food" })
///    → Response: { "message": "✅ Added Expense of 500...", "functionCalled": "add_expense", "functionResult": {...} }
/// 
/// 2. Add income:
///    { "message": "Got salary 3000000" }
///    → Calls: add_income({ amount: 3000000, description: "salary", category: "Salary" })
/// 
/// 3. Add investment:
///    { "message": "Bought Bitcoin 100000" }
///    → Calls: add_investment({ amount: 100000, description: "Bitcoin" })
/// 
/// 4. Add savings:
///    { "message": "Save 50000 to emergency fund" }
///    → Calls: add_savings({ amount: 50000, description: "emergency fund" })
/// 
/// 5. List transactions:
///    { "message": "Show my recent expenses" }
///    → Calls: list_transactions({ type: "Expense", limit: 10 })
/// 
/// 6. Monthly summary:
///    { "message": "Show my monthly summary" }
///    → Calls: get_monthly_summary({ month: "2026-03" })
/// 
/// 7. Yearly summary:
///    { "message": "How much did I spend this year?" }
///    → Calls: get_yearly_summary({ year: "2026" })
/// 
/// 8. Expense breakdown:
///    { "message": "Show expense breakdown by category" }
///    → Calls: get_expense_breakdown({ month: "2026-03" })
/// 
/// 9. List categories:
///    { "message": "What categories do I have?" }
///    → Calls: list_categories({})
/// 
/// 10. List recurring payments:
///     { "message": "Show my recurring bills" }
///     → Calls: list_recurring_payments({})
/// 
/// 11. Delete transaction:
///     { "message": "Delete transaction abc12345-..." }
///     → Calls: delete_transaction({ transaction_id: "abc12345-..." })
/// 
/// 12. Non-finance (rejected):
///     { "message": "How are you?" }
///     → No function call
///     → Response: { "message": "I'm here to help manage your finances 😊", "functionCalled": null }
/// 
/// 13. Unclear intent (asks clarification):
///     { "message": "Add 500" }
///     → No function call
///     → Response: { "message": "Is this an expense or income?", "functionCalled": null }
/// </summary>
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
        5. Never perform delete/update unless explicitly confirmed
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

    /// <summary>
    /// Main chat flow:
    /// 1. Build messages: [SystemPrompt + current date, UserMessage]
    /// 2. Send to OpenAI with 11 tool definitions
    /// 3. If OpenAI returns function_call → execute function → send result back to OpenAI → return natural language
    /// 4. If OpenAI returns text → return directly
    ///
    /// Example OpenAI request payload (what we send):
    /// {
    ///   "model": "gpt-4o-mini",
    ///   "messages": [
    ///     { "role": "system", "content": "You are an AI assistant...\nCurrent date: 2026-03-26" },
    ///     { "role": "user", "content": "Add coffee 500 yen" }
    ///   ],
    ///   "tools": [ ...11 function definitions... ]
    /// }
    ///
    /// Example OpenAI response (function call):
    /// {
    ///   "choices": [{
    ///     "finish_reason": "tool_calls",
    ///     "message": {
    ///       "tool_calls": [{
    ///         "id": "call_abc123",
    ///         "function": {
    ///           "name": "add_expense",
    ///           "arguments": "{\"amount\":500,\"description\":\"coffee\",\"category\":\"Food\"}"
    ///         }
    ///       }]
    ///     }
    ///   }]
    /// }
    ///
    /// Then we execute the function, send result back, and get final natural language response.
    /// </summary>
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

    /// <summary>
    /// Function Router — maps OpenAI function_call to actual backend services.
    /// 
    /// OpenAI sends: { "name": "add_expense", "arguments": "{\"amount\":500}" }
    /// We parse args → call the right service → return summary string + data object.
    /// The summary goes back to OpenAI for natural language formatting.
    /// The data object goes to the frontend as functionResult.
    /// </summary>
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
                "delete_transaction" => await DeleteTransactionAsync(userId, args),
                _ => ($"Unknown function: {functionName}", null)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing function {FunctionName}", functionName);
            return ($"Error executing {functionName}: {ex.Message}", null);
        }
    }

    // ============================================================================
    // FUNCTION IMPLEMENTATIONS
    // ============================================================================

    /// <summary>
    /// Add a transaction (expense/income/investment/savings).
    /// 
    /// Category resolution order:
    /// 1. If OpenAI provides category_id → use it directly
    /// 2. If OpenAI provides category name (e.g. "Food") → search by keyword
    /// 3. Fallback → use first category of matching type
    /// 4. No categories exist → return error message
    /// 
    /// Example: user says "Add coffee 500 yen"
    ///   → OpenAI args: { amount: 500, description: "coffee", category: "Food" }
    ///   → Search categories where type=Expense, keyword="Food"
    ///   → Found "Food & Drink" category → use its ID
    ///   → Create transaction via TransactionService
    ///   → Invalidate Redis cache
    ///   → Return: "✅ Added Expense of 500 on 2026-03-26 — coffee"
    /// </summary>
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

    // ============================================================================
    // HELPERS
    // ============================================================================

    private static string? ResolveRefreshTarget(IEnumerable<FunctionCallResult> functions)
    {
        var name = functions.FirstOrDefault()?.FunctionName;
        return name switch
        {
            "add_expense" or "add_income" or "add_investment" or "add_savings"
                or "list_transactions" or "delete_transaction" => AppConstants.ChatRefreshTarget.Transactions,
            "get_monthly_summary" or "get_yearly_summary"
                or "get_expense_breakdown" => AppConstants.ChatRefreshTarget.Summary,
            "list_categories" => AppConstants.ChatRefreshTarget.Categories,
            "list_recurring_payments" => AppConstants.ChatRefreshTarget.RecurringPayments,
            _ => null
        };
    }

    // ============================================================================
    // OPENAI FUNCTION/TOOL DEFINITIONS
    // ============================================================================

    /// <summary>
    /// Defines 11 functions that OpenAI can call.
    /// These are sent as the "tools" array in the OpenAI API request.
    /// OpenAI reads these definitions to decide WHEN and HOW to call each function.
    /// 
    /// Each tool has:
    ///   - name: function identifier (e.g. "add_expense")
    ///   - description: tells GPT when to use it
    ///   - parameters: JSON Schema defining expected arguments
    /// 
    /// Functions available:
    ///   add_expense        → "Add coffee 500 yen", "Spent 2000 on groceries"
    ///   add_income         → "Got salary 3000000", "Received freelance payment 500"
    ///   add_investment     → "Bought Bitcoin 100000", "Invested 50000 in stocks"
    ///   add_savings        → "Save 50000", "Deposit 10000 to savings"
    ///   list_transactions  → "Show my expenses", "List recent transactions"
    ///   get_monthly_summary → "Monthly summary", "How much did I spend this month?"
    ///   get_yearly_summary  → "Yearly report", "2026 summary"
    ///   get_expense_breakdown → "Spending by category", "Where does my money go?"
    ///   list_categories     → "What categories do I have?"
    ///   list_recurring_payments → "Show my subscriptions", "Recurring bills"
    ///   delete_transaction  → "Delete transaction {id}" (only when explicitly asked)
    /// </summary>
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
                "delete_transaction",
                "Delete a transaction by ID. Only use when user explicitly asks to delete and provides an ID.",
                BinaryData.FromString("""
                {
                    "type": "object",
                    "properties": {
                        "transaction_id": { "type": "string", "description": "The transaction UUID to delete" }
                    },
                    "required": ["transaction_id"]
                }
                """))
        ];
    }
}
