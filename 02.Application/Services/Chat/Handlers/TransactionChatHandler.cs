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
            return ("I couldn't map this to one of your categories. Please specify the category name exactly.", null);

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

    public async Task<(string, object?)> SumTransactionsAsync(Guid userId, JsonElement args)
    {
        var keyword = TryStr(args, "keyword") ?? TryStr(args, "description");
        if (string.IsNullOrWhiteSpace(keyword))
            return ("Please provide a keyword (e.g. merchant or description).", null);

        AppConstants.TransactionType? type = AppConstants.TransactionType.Expense;
        if (args.TryGetProperty("type", out var t) && Enum.TryParse<AppConstants.TransactionType>(t.GetString(), true, out var parsedType))
            type = parsedType;

        DateTime? startDate = null;
        DateTime? endDate = null;
        var startStr = TryStr(args, "start_date");
        var endStr = TryStr(args, "end_date");
        if (!string.IsNullOrEmpty(startStr) && DateTime.TryParse(startStr, out var sd))
            startDate = EnsureUtc(sd).Date;
        if (!string.IsNullOrEmpty(endStr) && DateTime.TryParse(endStr, out var ed))
            endDate = EnsureUtc(ed).Date.AddDays(1).AddTicks(-1);

        // Default to current calendar month when no range given
        if (startDate is null && endDate is null)
        {
            var now = DateTime.UtcNow;
            startDate = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            endDate = startDate.Value.AddMonths(1).AddTicks(-1);
        }

        decimal total = 0;
        var count = 0;
        DateTime? cursor = null;
        Guid? cursorId = null;
        const int maxPages = 5;

        for (var page = 0; page < maxPages; page++)
        {
            var filter = new TransactionFilterRequest
            {
                Keyword = keyword,
                Type = type,
                StartDate = startDate,
                EndDate = endDate,
                PageSize = 100,
                Cursor = cursor,
                CursorId = cursorId
            };

            var result = await _transactionService.GetTransactionsAsync(userId, filter);
            total += result.Items.Sum(tx => tx.Amount);
            count += result.Items.Count;

            if (!result.HasNextPage || result.Items.Count == 0)
                break;

            cursor = result.NextCursor;
            cursorId = result.NextCursorId;
        }

        var rangeLabel = startDate is not null && endDate is not null
            ? $"{startDate:yyyy-MM-dd} → {endDate:yyyy-MM-dd}"
            : "all dates";

        if (count == 0)
            return ($"No {type} matching '{keyword}' for {rangeLabel}.", new { keyword, total = 0m, count = 0 });

        var summary = $"'{keyword}' ({type}): {total:N0} across {count} transaction(s) ({rangeLabel}).";
        return (summary, new { keyword, type = type.ToString(), total, count, startDate, endDate });
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
        var explicitCategoryName = TryStr(args, "category");
        var description = TryStr(args, "description");

        if (string.IsNullOrEmpty(categoryId) && args.TryGetProperty("category", out var catName))
        {
            var resolved = await ResolveCategoryByNameAsync(userId, catName.GetString(), type);
            if (!string.IsNullOrEmpty(resolved)) return resolved;
        }

        if (string.IsNullOrEmpty(categoryId))
        {
            // If user did not specify category, try infer from description against known categories.
            if (string.IsNullOrWhiteSpace(explicitCategoryName))
            {
                var inferred = await InferCategoryIdFromDescriptionAsync(userId, type, description);
                if (!string.IsNullOrEmpty(inferred))
                    return inferred;
            }
        }

        return categoryId;
    }

    private async Task<string?> ResolveCategoryByNameAsync(Guid userId, string? categoryName, AppConstants.TransactionType type)
    {
        if (string.IsNullOrEmpty(categoryName)) return null;

        var categories = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest
        {
            Type = type,
            PageSize = 100
        });
        if (categories.Items.Count == 0) return null;

        var target = Normalize(categoryName);
        var exact = categories.Items.FirstOrDefault(c => Normalize(c.DisplayName) == target);
        if (exact is not null)
            return exact.CategoryId.ToString();

        var contains = categories.Items.FirstOrDefault(c =>
            Normalize(c.DisplayName).Contains(target, StringComparison.Ordinal) ||
            target.Contains(Normalize(c.DisplayName), StringComparison.Ordinal));
        return contains?.CategoryId.ToString();
    }

    private async Task<string?> InferCategoryIdFromDescriptionAsync(Guid userId, AppConstants.TransactionType type, string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        var categories = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest
        {
            Type = type,
            PageSize = 100
        });
        if (categories.Items.Count == 0) return null;

        var descTokens = Tokenize(description);
        if (descTokens.Count == 0) return null;

        var scored = categories.Items
            .Select(c =>
            {
                var catTokens = Tokenize(c.DisplayName);
                var score = catTokens.Count == 0 ? 0 : catTokens.Count(t => descTokens.Contains(t));
                return (Category: c, Score: score);
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Category.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (scored.Count == 0) return null;
        if (scored.Count > 1 && scored[0].Score == scored[1].Score) return null; // ambiguous, ask user

        return scored[0].Category.CategoryId.ToString();
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

    private static string Normalize(string value) =>
        string.Concat(value.Trim().ToLowerInvariant().Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch)));

    private static HashSet<string> Tokenize(string value)
    {
        var normalized = Normalize(value);
        var parts = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length >= 2);
        return parts.ToHashSet(StringComparer.Ordinal);
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
