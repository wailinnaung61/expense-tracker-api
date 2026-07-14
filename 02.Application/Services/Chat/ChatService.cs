using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Application.Services.Chat.Handlers;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;

namespace expense_tracker_backend.Application.Services.Chat;

public class ChatService : IChatService
{
    private readonly ChatClient _chatClient;
    private readonly ILogger<ChatService> _logger;
    private readonly ChatContextLoader _contextLoader;
    private readonly ChatHistoryStore _historyStore;
    private readonly ChatPreFilter _preFilter;
    private readonly ChatSystemPromptBuilder _promptBuilder;
    private readonly ChatToolRegistry _toolRegistry;
    private readonly ChatRefreshResolver _refreshResolver;
    private readonly ChatConfirmationHandler _confirmationHandler;

    private readonly TransactionChatHandler _transactionHandler;
    private readonly CategoryChatHandler _categoryHandler;
    private readonly BudgetChatHandler _budgetHandler;
    private readonly RecurringPaymentChatHandler _recurringHandler;
    private readonly SavingGoalChatHandler _savingHandler;
    private readonly InvestmentChatHandler _investmentHandler;
    private readonly AggregationChatHandler _aggregationHandler;

    private const int MaxToolCallIterations = 5;
    private static readonly Regex MutationIntentPattern = new(
        @"\b(add|create|record|log|spent|spend|paid|pay|invested|invest|save|saved|contribute|contribution|update|edit|change|delete|remove)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ChatService(
        ILogger<ChatService> logger,
        IConfiguration configuration,
        ChatContextLoader contextLoader,
        ChatHistoryStore historyStore,
        ChatPreFilter preFilter,
        ChatSystemPromptBuilder promptBuilder,
        ChatToolRegistry toolRegistry,
        ChatRefreshResolver refreshResolver,
        ChatConfirmationHandler confirmationHandler,
        TransactionChatHandler transactionHandler,
        CategoryChatHandler categoryHandler,
        BudgetChatHandler budgetHandler,
        RecurringPaymentChatHandler recurringHandler,
        SavingGoalChatHandler savingHandler,
        InvestmentChatHandler investmentHandler,
        AggregationChatHandler aggregationHandler)
    {
        _logger = logger;
        _contextLoader = contextLoader;
        _historyStore = historyStore;
        _preFilter = preFilter;
        _promptBuilder = promptBuilder;
        _toolRegistry = toolRegistry;
        _refreshResolver = refreshResolver;
        _confirmationHandler = confirmationHandler;
        _transactionHandler = transactionHandler;
        _categoryHandler = categoryHandler;
        _budgetHandler = budgetHandler;
        _recurringHandler = recurringHandler;
        _savingHandler = savingHandler;
        _investmentHandler = investmentHandler;
        _aggregationHandler = aggregationHandler;

        var apiKey = configuration["OpenAI:ApiKey"]
            ?? throw new InvalidOperationException("OpenAI:ApiKey is not configured");
        var model = configuration["OpenAI:Model"] ?? "gpt-4o-mini";
        _chatClient = new ChatClient(model, apiKey);
    }

    public async Task<ChatInitResponse> InitAsync(Guid userId)
    {
        var context = await _contextLoader.LoadAsync(userId);
        return _contextLoader.ToInitResponse(context);
    }

    public async Task ClearHistoryAsync(Guid userId)
    {
        await _historyStore.ClearHistoryAsync(userId);
        await _historyStore.RemovePendingAsync(userId);
    }

    public async Task<ChatResponse> ChatAsync(Guid userId, string message)
    {
        var context = await _contextLoader.LoadAsync(userId);
        var userName = context.UserName;

        // [1] Check pending confirmation
        var confirmResult = await _confirmationHandler.TryHandleAsync(userId, message, userName);
        if (confirmResult is not null)
        {
            if (_refreshResolver.Resolve(confirmResult.FunctionsCalled ?? []) is not null)
                await _contextLoader.InvalidateAsync(userId);
            return confirmResult;
        }

        // [2] Pre-filter: quick response
        var quickResponse = await _preFilter.TryQuickResponseAsync(userId, message, userName);
        if (quickResponse is not null)
            return quickResponse;

        // [3] Pre-filter: direct command shortcut
        var directCommand = _preFilter.TryParseDirectCommand(message);
        if (directCommand is not null)
        {
            var directResult = await ExecuteDirectCommandAsync(userId, directCommand, userName);
            if (directResult is not null) return directResult;
        }

        // [4] Full OpenAI flow — wrapped with retry-safe error handling
        try
        {
            return await ExecuteOpenAiFlowAsync(userId, message, context, userName);
        }
        catch (Exception ex) when (ex is AggregateException or HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "OpenAI connection failed for user {UserId}", userId);
            return new ChatResponse(
                "Sorry, the AI service is temporarily unavailable. Please try again in a moment.",
                userName, null, null, DateTime.UtcNow);
        }
    }

    private async Task<ChatResponse?> ExecuteDirectCommandAsync(Guid userId, DirectCommandMatch command, string? userName)
    {
        try
        {
            var moneyAmounts = command.GetMoneyAmounts();
            if (moneyAmounts.Count > 0)
            {
                var functionsCalled = new List<FunctionCallResult>();
                var summaries = new List<string>();
                ChatClientAction? clientAction = null;

                foreach (var amt in moneyAmounts)
                {
                    var payloadJson =
                        $"{{\"amount\":{amt},\"description\":\"{EscapeJson(command.Description)}\",\"category\":\"{EscapeJson(command.Category)}\"}}";
                    using var doc = JsonDocument.Parse(payloadJson);
                    var args = doc.RootElement.Clone();
                    var (summary, data) = await ExecuteFunctionAsync(userId, command.FunctionName, args);
                    summaries.Add(summary);
                    functionsCalled.Add(new FunctionCallResult(command.FunctionName, data is ChatClientAction ? null : data));
                    if (data is ChatClientAction ca)
                        clientAction = ca;
                }

                var refreshTarget = _refreshResolver.Resolve(functionsCalled);
                if (refreshTarget is not null)
                    await _contextLoader.InvalidateAsync(userId);

                var combinedSummary = string.Join(" ", summaries);
                return new ChatResponse(combinedSummary, userName, refreshTarget, functionsCalled, DateTime.UtcNow, clientAction);
            }

            const string emptyArgsJson = "{}";
            using var doc2 = JsonDocument.Parse(emptyArgsJson);
            var args2 = doc2.RootElement.Clone();

            var (summary2, data2) = await ExecuteFunctionAsync(userId, command.FunctionName, args2);
            var functionsCalled2 = new List<FunctionCallResult> { new(command.FunctionName, data2 is ChatClientAction ? null : data2) };
            var refreshTarget2 = _refreshResolver.Resolve(functionsCalled2);
            var clientAction2 = data2 as ChatClientAction;

            if (refreshTarget2 is not null)
                await _contextLoader.InvalidateAsync(userId);

            return new ChatResponse(summary2, userName, refreshTarget2, functionsCalled2, DateTime.UtcNow, clientAction2);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Direct command failed: {FunctionName}", command.FunctionName);
            return null; // Fall through to OpenAI
        }
    }

    private async Task<ChatResponse> ExecuteOpenAiFlowAsync(Guid userId, string message, ChatContextSnapshot context, string? userName)
    {
        var tools = _toolRegistry.BuildAll();
        var systemMessage = _promptBuilder.Build(context);
        var history = await _historyStore.GetHistoryAsync(userId);

        var messages = new List<ChatMessage>(history.Count + 2) { new SystemChatMessage(systemMessage) };
        messages.AddRange(history);
        messages.Add(new UserChatMessage(message));

        var options = new ChatCompletionOptions();
        foreach (var tool in tools)
            options.Tools.Add(tool);

        _logger.LogInformation("OpenAI request for user {UserId}: {Message} (history: {Count} msgs)",
            userId, message, history.Count);

        var response = await _chatClient.CompleteChatAsync(messages, options);
        var choice = response.Value;

        var functionsCalled = new List<FunctionCallResult>();
        ChatClientAction? clientActionFromTools = null;
        var iterations = 0;

        while (choice.FinishReason == ChatFinishReason.ToolCalls && iterations < MaxToolCallIterations)
        {
            iterations++;
            messages.Add(new AssistantChatMessage(choice));

            var primaryCall = choice.ToolCalls[0];
            foreach (var extra in choice.ToolCalls.Skip(1))
            {
                _logger.LogWarning("Ignoring extra tool call: {FunctionName}", extra.FunctionName);
                messages.Add(new ToolChatMessage(extra.Id, "Only one function per step is allowed."));
            }

            _logger.LogInformation("Tool call [{Iter}]: {FunctionName}", iterations, primaryCall.FunctionName);

            var argsJson = primaryCall.FunctionArguments.ToString();
            if (!IsValidJson(argsJson))
            {
                _logger.LogError("Invalid JSON args for {FunctionName}: {Args}", primaryCall.FunctionName, argsJson);
                messages.Add(new ToolChatMessage(primaryCall.Id, "Invalid arguments. Please try again."));
            }
            else
            {
                if (_refreshResolver.IsMutation(primaryCall.FunctionName) && !HasExplicitMutationIntent(message))
                {
                    _logger.LogWarning(
                        "Blocked mutation tool call {FunctionName} due to non-mutation user intent. Message: {Message}",
                        primaryCall.FunctionName, message);
                    var blockedText = BuildBlockedMutationMessage(message, context);
                    messages.Add(new ToolChatMessage(
                        primaryCall.Id,
                        "Mutation blocked: user did not explicitly ask to add/update/delete data."));
                    messages.Add(new AssistantChatMessage(blockedText));
                    await _historyStore.SaveHistoryAsync(userId, messages.Skip(1).ToList());
                    return new ChatResponse(blockedText, userName, null, null, DateTime.UtcNow);
                }

                using var doc = JsonDocument.Parse(argsJson);
                var args = doc.RootElement.Clone();
                var (result, resultObj) = await ExecuteFunctionAsync(userId, primaryCall.FunctionName, args);
                if (resultObj is ChatClientAction ca)
                    clientActionFromTools = ca;
                messages.Add(new ToolChatMessage(primaryCall.Id, result ?? string.Empty));
                functionsCalled.Add(new FunctionCallResult(primaryCall.FunctionName, resultObj is ChatClientAction ? null : resultObj));
            }

            var next = await _chatClient.CompleteChatAsync(messages, options);
            choice = next.Value;
        }

        ChatResponse chatResponse;
        if (functionsCalled.Count > 0)
        {
            var finalText = choice.Content.FirstOrDefault()?.Text
                ?? string.Join("\n", functionsCalled.Select(f => f.FunctionName));
            messages.Add(new AssistantChatMessage(choice));
            var refreshTarget = _refreshResolver.Resolve(functionsCalled);
            chatResponse = new ChatResponse(finalText, userName, refreshTarget, functionsCalled, DateTime.UtcNow, clientActionFromTools);

            if (refreshTarget is not null)
                await _contextLoader.InvalidateAsync(userId);
        }
        else
        {
            var text = choice.Content.FirstOrDefault()?.Text ?? "I'm sorry, I couldn't process your request.";
            messages.Add(new AssistantChatMessage(choice));
            chatResponse = new ChatResponse(text, userName, null, null, DateTime.UtcNow);
        }

        await _historyStore.SaveHistoryAsync(userId, messages.Skip(1).ToList());
        return chatResponse;
    }

    private async Task<(string Summary, object? Data)> ExecuteFunctionAsync(Guid userId, string functionName, JsonElement args)
    {
        try
        {
            return functionName switch
            {
                "add_expense" => await _transactionHandler.AddTransactionAsync(userId, AppConstants.TransactionType.Expense, args),
                "add_income" => await _transactionHandler.AddTransactionAsync(userId, AppConstants.TransactionType.Income, args),
                "add_investment" => await _transactionHandler.AddTransactionAsync(userId, AppConstants.TransactionType.Investment, args),
                "add_savings" => await _transactionHandler.AddTransactionAsync(userId, AppConstants.TransactionType.Savings, args),
                "update_transaction" => await _transactionHandler.UpdateTransactionAsync(userId, args),
                "list_transactions" => await _transactionHandler.ListTransactionsAsync(userId, args),
                "find_transaction" => await _transactionHandler.FindTransactionAsync(userId, args),
                "delete_transaction" => await _transactionHandler.DeleteTransactionAsync(userId, args),
                "find_and_delete_transaction" => await _transactionHandler.FindAndDeleteTransactionAsync(userId, args),

                "list_categories" => await _categoryHandler.ListCategoriesAsync(userId, args),
                "create_category" => await _categoryHandler.CreateCategoryAsync(userId, args),
                "update_category" => await _categoryHandler.UpdateCategoryAsync(userId, args),
                "delete_category" => await _categoryHandler.DeleteCategoryAsync(userId, args),

                "get_budget" => await _budgetHandler.GetBudgetAsync(userId, args),
                "get_budget_range" => await _budgetHandler.GetBudgetByRangeAsync(userId, args),
                "get_budget_containing" => await _budgetHandler.GetBudgetContainingDateAsync(userId, args),
                "create_budget" => await _budgetHandler.CreateBudgetAsync(userId, args),
                "update_budget" => await _budgetHandler.UpdateBudgetAsync(userId, args),
                "delete_budget" => await _budgetHandler.DeleteBudgetAsync(userId, args),
                "add_budget_category" => await _budgetHandler.AddBudgetCategoryAsync(userId, args),
                "remove_budget_category" => await _budgetHandler.RemoveBudgetCategoryAsync(userId, args),

                "list_recurring_payments" => await _recurringHandler.ListAsync(userId),
                "create_recurring_payment" => await _recurringHandler.CreateAsync(userId, args),
                "update_recurring_payment" => await _recurringHandler.UpdateAsync(userId, args),
                "delete_recurring_payment" => await _recurringHandler.DeleteAsync(userId, args),
                "mark_recurring_paid" => await _recurringHandler.MarkAsPaidAsync(userId, args),
                "acknowledge_recurring_paid" => await _recurringHandler.AcknowledgePaidAsync(userId, args),

                "list_saving_goals" => await _savingHandler.ListGoalsAsync(userId, args),
                "create_saving_goal" => await _savingHandler.CreateGoalAsync(userId, args),
                "update_saving_goal" => await _savingHandler.UpdateGoalAsync(userId, args),
                "delete_saving_goal" => await _savingHandler.DeleteGoalAsync(userId, args),
                "add_saving_contribution" => await _savingHandler.AddContributionAsync(userId, args),
                "get_saving_dashboard" => await _savingHandler.GetDashboardAsync(userId),

                "list_portfolios" => await _investmentHandler.ListPortfoliosAsync(userId),
                "create_portfolio" => await _investmentHandler.CreatePortfolioAsync(userId, args),
                "update_portfolio" => await _investmentHandler.UpdatePortfolioAsync(userId, args),
                "delete_portfolio" => await _investmentHandler.DeletePortfolioAsync(userId, args),

                "list_investments" => await _investmentHandler.ListInvestmentsAsync(userId, args),
                "create_investment_record" => await _investmentHandler.CreateAsync(userId, args),
                "update_investment" => await _investmentHandler.UpdateAsync(userId, args),
                "delete_investment" => await _investmentHandler.DeleteAsync(userId, args),
                "get_investment_dashboard" => await _investmentHandler.GetDashboardAsync(userId),

                "get_monthly_summary" => await _aggregationHandler.GetMonthlySummaryAsync(userId, args),
                "get_yearly_summary" => await _aggregationHandler.GetYearlySummaryAsync(userId, args),
                "get_expense_breakdown" => await _aggregationHandler.GetExpenseBreakdownAsync(userId, args),
                "get_custom_date_range" => await _aggregationHandler.GetCustomDateRangeAsync(userId, args),
                "get_dashboard" => await _aggregationHandler.GetDashboardAsync(userId, args),
                "get_dashboard_range" => await _aggregationHandler.GetDashboardByRangeAsync(userId, args),

                "suggest_reports_download" => (
                    ChatReportsDownloadIntent.AssistantMessage,
                    ChatReportsDownloadIntent.BuildClientActionFromToolArgs(args)),

                _ => ($"Unknown function: {functionName}", null)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing {FunctionName}", functionName);
            return ($"Error executing {functionName}: {ex.Message}", null);
        }
    }

    private static bool IsValidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { using var doc = JsonDocument.Parse(json); return true; }
        catch { return false; }
    }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static bool HasExplicitMutationIntent(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return false;
        return MutationIntentPattern.IsMatch(message);
    }

    private static string BuildBlockedMutationMessage(string message, ChatContextSnapshot context)
    {
        var categories = context.Categories
            .Where(c => c.Type == AppConstants.TransactionType.Expense.ToString())
            .Select(c => c.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (message.Contains("category", StringComparison.OrdinalIgnoreCase) && categories.Count > 0)
        {
            var names = string.Join(", ", categories.Take(10));
            return $"Yes — I know your expense categories. {names}.";
        }

        return "I didn't add anything. Ask with an explicit command like 'add expense 1200 groceries' if you want to record data.";
    }
}
