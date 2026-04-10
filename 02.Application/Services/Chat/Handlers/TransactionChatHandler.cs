using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services.Chat.Handlers;

public class TransactionChatHandler
{
    private readonly ITranactionService _transactionService;
    private readonly IExpenseCategoryService _categoryService;
    private readonly ChatHistoryStore _historyStore;

    public TransactionChatHandler(
        ITranactionService transactionService,
        IExpenseCategoryService categoryService,
        ChatHistoryStore historyStore)
    {
        _transactionService = transactionService;
        _categoryService = categoryService;
        _historyStore = historyStore;
    }

    public async Task<(string, object?)> AddTransactionAsync(Guid userId, AppConstants.TransactionType type, JsonElement args)
    {
        if (!args.TryGetProperty("amount", out var amtVal) || !amtVal.TryGetDecimal(out var amount) || amount <= 0)
            return ("Please provide the amount.", null);

        var description = TryStr(args, "description") ?? "";
        var categoryId = TryStr(args, "category_id") ?? "";
        var date = TryStr(args, "date") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        var statusStr = TryStr(args, "status") ?? "Completed";
        var status = statusStr.Equals("Pending", StringComparison.OrdinalIgnoreCase)
            ? AppConstants.PaymentStatus.Pending
            : AppConstants.PaymentStatus.Completed;

        categoryId = await ResolveCategoryIdAsync(userId, categoryId, type, args);
        if (string.IsNullOrEmpty(categoryId))
            return ("No categories found. Please create a category first.", null);

        var dto = new CreateTranactionDto(type, categoryId, amount, date, status, description, "", "");
        var result = await _transactionService.CreateTranactionAsync(dto, userId);

        var summary = $"Added {type} of {amount:N0} on {date}";
        if (!string.IsNullOrEmpty(description)) summary += $" — {description}";

        return (summary, result);
    }

    public async Task<(string, object?)> UpdateTransactionAsync(Guid userId, JsonElement args)
    {
        var idStr = args.TryGetProperty("transaction_id", out var id) ? id.GetString() : null;
        Guid transactionId;

        // 1) Prefer explicit transaction ID
        if (!string.IsNullOrEmpty(idStr) && Guid.TryParse(idStr, out var parsedId))
        {
            transactionId = parsedId;
        }
        else
        {
            // 2) Reuse latest matched transaction from pending cache (from previous find step)
            var pending = await _historyStore.GetPendingAsync(userId);
            if (pending?.TransactionIds.Count == 1)
            {
                transactionId = pending.TransactionIds[0];
                await _historyStore.RemovePendingAsync(userId);
            }
            else
            {
                // 3) Fallback: locate by match details
                var findResult = await FindTransactionsForUpdateAsync(userId, args);
                if (findResult.Error is not null) return (findResult.Error, null);
                if (findResult.Matches.Count == 0)
                    return ("I couldn't find a matching transaction to update. Please include date/description/current amount.", null);
                if (findResult.Matches.Count > 1)
                {
                    var lines = findResult.Matches.Select(tx =>
                        $"• {tx.Amount:N0} | {tx.TranactionDate} | {tx.Description} | ID: {tx.TranactionId}");
                    return ($"I found multiple matches. Please specify one:\n{string.Join("\n", lines)}", findResult.Matches);
                }

                transactionId = findResult.Matches[0].TranactionId;
            }
        }

        var existing = await _transactionService.GetTranactionByIdAsync(userId, transactionId);
        if (existing is null)
            return ("Transaction not found.", null);

        var amount = args.TryGetProperty("new_amount", out var na)
            ? na.GetDecimal()
            : args.TryGetProperty("amount", out var a)
                ? a.GetDecimal()
                : existing.Amount;
        var description = args.TryGetProperty("description", out var desc) ? desc.GetString() ?? existing.Description : existing.Description;
        var date = args.TryGetProperty("date", out var d) ? d.GetString() ?? existing.TranactionDate : existing.TranactionDate;
        var categoryId = args.TryGetProperty("category_id", out var cat) ? cat.GetString() ?? existing.CategoryId : existing.CategoryId;
        var statusStr = args.TryGetProperty("status", out var s) ? s.GetString() : null;
        var status = statusStr switch
        {
            not null when statusStr.Equals("Pending", StringComparison.OrdinalIgnoreCase) => AppConstants.PaymentStatus.Pending,
            not null => AppConstants.PaymentStatus.Completed,
            _ => existing.status
        };

        if (args.TryGetProperty("category", out var catName) && !args.TryGetProperty("category_id", out _))
        {
            var resolved = await ResolveCategoryByNameAsync(userId, catName.GetString(), existing.type);
            if (!string.IsNullOrEmpty(resolved)) categoryId = resolved;
        }

        var dto = new UpdateTranactionDto(existing.type, categoryId, amount, date, status, description, "", "");
        var result = await _transactionService.UpdateTranactionAsync(userId, transactionId, dto);

        return result is not null
            ? ($"Updated transaction: {amount:N0} on {date}", result)
            : ("Failed to update transaction.", null);
    }

    public async Task<(string, object?)> ListTransactionsAsync(Guid userId, JsonElement args)
    {
        AppConstants.TransactionType? type = null;
        if (args.TryGetProperty("type", out var t))
        {
            var typeStr = t.GetString();
            if (Enum.TryParse<AppConstants.TransactionType>(typeStr, true, out var parsed))
                type = parsed;
        }

        var pageSize = args.TryGetProperty("limit", out var l) && l.TryGetInt32(out var lVal) ? lVal : 10;
        var keyword = TryStr(args, "keyword");

        DateTime? startDate = null;
        DateTime? endDate = null;
        var startStr = TryStr(args, "start_date");
        var endStr = TryStr(args, "end_date");
        if (!string.IsNullOrEmpty(startStr) && DateTime.TryParse(startStr, out var sd))
            startDate = EnsureUtc(sd);
        if (!string.IsNullOrEmpty(endStr) && DateTime.TryParse(endStr, out var ed))
            endDate = EnsureUtc(ed).Date.AddDays(1).AddTicks(-1);

        var filter = new TransactionFilterRequest
        {
            Type = type,
            Keyword = keyword,
            StartDate = startDate,
            EndDate = endDate,
            PageSize = Math.Clamp(pageSize, 1, 20)
        };

        var result = await _transactionService.GetTransactionsAsync(userId, filter);

        if (result.Items.Count == 0)
            return ("No transactions found.", result);

        var lines = result.Items.Select(tx =>
            $"• {tx.type} | {tx.Amount:N0} | {tx.TranactionDate} | {tx.Description}");
        var summary = $"Found {result.TotalCount} transaction(s):\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    public async Task<(string, object?)> FindTransactionAsync(Guid userId, JsonElement args)
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
            var utcDate = EnsureUtc(parsedDate).Date;
            startDate = utcDate;
            endDate = utcDate.AddDays(1).AddTicks(-1);
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
        var summary = $"Found {matches.Count} matching transaction(s):\n{string.Join("\n", lines)}\n\nDo you want to delete this? Reply 'delete' to confirm.";

        var pending = new PendingConfirmation(
            "delete",
            matches.Select(tx => tx.TranactionId).ToList(),
            string.Join(", ", matches.Select(tx => $"{tx.Amount:N0} — {tx.Description} on {tx.TranactionDate}"))
        );
        await _historyStore.SetPendingAsync(userId, pending);

        return (summary, null);
    }

    public async Task<(string, object?)> DeleteTransactionAsync(Guid userId, JsonElement args)
    {
        var idStr = args.TryGetProperty("transaction_id", out var id) ? id.GetString() : null;
        if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out var transactionId))
            return ("Please provide a valid transaction ID to delete.", null);

        var result = await _transactionService.DeleteTranactionAsync(userId, transactionId);
        return result
            ? ("Transaction deleted successfully.", true)
            : ("Transaction not found.", false);
    }

    public async Task<(string, object?)> FindAndDeleteTransactionAsync(Guid userId, JsonElement args)
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
            var utcDate = EnsureUtc(parsedDate).Date;
            startDate = utcDate;
            endDate = utcDate.AddDays(1).AddTicks(-1);
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

        if (matches.Count > 1)
        {
            var lines = matches.Select(tx =>
                $"• {tx.Amount:N0} | {tx.TranactionDate} | {tx.Description} | ID: {tx.TranactionId}");
            return ($"Found {matches.Count} matching transactions. Please specify which one:\n{string.Join("\n", lines)}", matches);
        }

        var match = matches[0];
        var deleted = await _transactionService.DeleteTranactionAsync(userId, match.TranactionId);
        return deleted
            ? ($"Deleted: {match.Amount:N0} — {match.Description} on {match.TranactionDate}", match)
            : ("Failed to delete transaction.", null);
    }

    private async Task<string> ResolveCategoryIdAsync(Guid userId, string categoryId, AppConstants.TransactionType type, JsonElement args)
    {
        if (string.IsNullOrEmpty(categoryId) && args.TryGetProperty("category", out var catName))
        {
            var resolved = await ResolveCategoryByNameAsync(userId, catName.GetString(), type);
            if (!string.IsNullOrEmpty(resolved)) return resolved;
        }

        if (string.IsNullOrEmpty(categoryId))
        {
            var categories = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest { Type = type, PageSize = 1 });
            if (categories.Items.Count > 0)
                return categories.Items[0].CategoryId.ToString();
        }

        return categoryId;
    }

    private async Task<string?> ResolveCategoryByNameAsync(Guid userId, string? categoryName, AppConstants.TransactionType type)
    {
        if (string.IsNullOrEmpty(categoryName)) return null;

        var categories = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest
        {
            Type = type,
            Keyword = categoryName,
            PageSize = 1
        });
        return categories.Items.Count > 0 ? categories.Items[0].CategoryId.ToString() : null;
    }

    private static string? TryStr(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static DateTime EnsureUtc(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
    }

    private async Task<(List<Tranaction> Matches, string? Error)> FindTransactionsForUpdateAsync(Guid userId, JsonElement args)
    {
        var keyword = args.TryGetProperty("match_description", out var md)
            ? md.GetString()
            : args.TryGetProperty("description", out var d) ? d.GetString() : null;

        var oldAmount = args.TryGetProperty("old_amount", out var oa)
            ? oa.GetDecimal()
            : args.TryGetProperty("match_amount", out var ma) ? ma.GetDecimal() : (decimal?)null;

        // If AI only passes "amount" and also "new_amount", treat "amount" as old amount.
        if (!oldAmount.HasValue && args.TryGetProperty("new_amount", out _) && args.TryGetProperty("amount", out var a))
            oldAmount = a.GetDecimal();

        var date = args.TryGetProperty("match_date", out var mdt)
            ? mdt.GetString()
            : args.TryGetProperty("date", out var dt) ? dt.GetString() : null;

        AppConstants.TransactionType? type = null;
        if (args.TryGetProperty("type", out var t) && Enum.TryParse<AppConstants.TransactionType>(t.GetString(), true, out var parsedType))
            type = parsedType;

        DateTime? startDate = null;
        DateTime? endDate = null;
        if (!string.IsNullOrEmpty(date) && DateTime.TryParse(date, out var parsedDate))
        {
            var utcDate = EnsureUtc(parsedDate).Date;
            startDate = utcDate;
            endDate = utcDate.AddDays(1).AddTicks(-1);
        }

        var filter = new TransactionFilterRequest
        {
            Keyword = keyword,
            Type = type,
            StartDate = startDate,
            EndDate = endDate,
            PageSize = 20
        };

        var result = await _transactionService.GetTransactionsAsync(userId, filter);
        var matches = oldAmount.HasValue
            ? result.Items.Where(tx => tx.Amount == oldAmount.Value).ToList()
            : result.Items.ToList();

        return (matches, null);
    }
}
