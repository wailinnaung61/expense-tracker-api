using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services.Chat;

public class ChatConfirmationHandler
{
    private readonly ITranactionService _transactionService;
    private readonly ChatHistoryStore _historyStore;

    public ChatConfirmationHandler(
        ITranactionService transactionService,
        ChatHistoryStore historyStore)
    {
        _transactionService = transactionService;
        _historyStore = historyStore;
    }

    public static bool IsDeleteConfirmation(string message)
    {
        var m = message.Trim().ToLowerInvariant();
        return m is "delete" or "delete it" or "confirm delete" or "yes delete" or "proceed delete";
    }

    public async Task<ChatResponse?> TryHandleAsync(Guid userId, string message, string? userName)
    {
        if (!IsDeleteConfirmation(message)) return null;

        var pending = await _historyStore.GetPendingAsync(userId);
        if (pending is null) return null;

        await _historyStore.RemovePendingAsync(userId);

        string responseText;
        List<FunctionCallResult> results = [];

        if (pending.Action == "delete")
        {
            var deleted = 0;
            foreach (var txId in pending.TransactionIds)
            {
                if (await _transactionService.DeleteTranactionAsync(userId, txId))
                {
                    results.Add(new FunctionCallResult("delete_transaction", txId));
                    deleted++;
                }
            }
            responseText = deleted > 0
                ? $"🗑️ Deleted: {pending.Summary}"
                : "Transaction not found or already deleted.";
        }
        else
        {
            responseText = "Action cancelled.";
        }

        var refreshTarget = results.Count > 0 ? AppConstants.ChatRefreshTarget.Transactions : null;
        return new ChatResponse(responseText, userName, refreshTarget, results, DateTime.UtcNow);
    }
}
