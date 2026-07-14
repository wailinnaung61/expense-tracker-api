using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services.Chat.Handlers;

public class RecurringPaymentChatHandler
{
    private readonly IRecurringPaymentService _recurringService;
    private readonly IExpenseCategoryService _categoryService;

    public RecurringPaymentChatHandler(
        IRecurringPaymentService recurringService,
        IExpenseCategoryService categoryService)
    {
        _recurringService = recurringService;
        _categoryService = categoryService;
    }

    public async Task<(string, object?)> ListAsync(Guid userId)
    {
        var result = await _recurringService.GetAllAsync(userId);

        if (result.Count == 0)
            return ("No recurring payments found.", result);

        var lines = result.Select(r =>
            $"• {r.Name}: {r.Amount:N0} ({r.Frequency}) — Next: {r.NextDueDate:yyyy-MM-dd} [{r.Status}]");
        var summary = $"Recurring Payments:\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    public async Task<(string, object?)> CreateAsync(Guid userId, JsonElement args)
    {
        var name = TryStr(args, "name");
        if (string.IsNullOrWhiteSpace(name))
            return ("Please provide a name for the recurring payment (e.g. Netflix, Rent).", null);

        var amount = TryDecimal(args, "amount");
        if (amount <= 0)
            return ("Please provide the payment amount.", null);

        var frequencyStr = TryStr(args, "frequency") ?? "Monthly";
        if (!Enum.TryParse<AppConstants.RecurringFrequency>(frequencyStr, true, out var frequency))
            frequency = AppConstants.RecurringFrequency.Monthly;

        var nextDueDate = TryStr(args, "next_due_date") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        if (!DateTime.TryParse(nextDueDate, out var parsedDate))
            parsedDate = DateTime.UtcNow;
        parsedDate = EnsureUtc(parsedDate);

        var categoryId = await ResolveCategoryIdAsync(userId, args);
        if (string.IsNullOrEmpty(categoryId))
            return ("No matching category found. Please create a category first.", null);

        var payment = new RecurringPayment
        {
            RecurringId = Guid.NewGuid().ToString(),
            UserId = userId.ToString(),
            Name = name,
            Amount = amount,
            CategoryId = categoryId,
            Frequency = frequency,
            NextDueDate = parsedDate
        };

        var result = await _recurringService.CreateAsync(userId, payment);
        return ($"Created recurring payment: {result.Name} — {result.Amount:N0} ({result.Frequency})", result);
    }

    public async Task<(string, object?)> UpdateAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolveRecurringAsync(userId, args);
        if (existing is null)
            return ("Recurring payment not found. Try 'list recurring' to see your payments.", null);

        if (args.TryGetProperty("name", out var n)) existing.Name = n.GetString() ?? existing.Name;
        if (args.TryGetProperty("amount", out var a)) existing.Amount = a.GetDecimal();
        if (args.TryGetProperty("frequency", out var f) && Enum.TryParse<AppConstants.RecurringFrequency>(f.GetString(), true, out var freq))
            existing.Frequency = freq;
        if (args.TryGetProperty("next_due_date", out var d) && DateTime.TryParse(d.GetString(), out var parsedDate))
            existing.NextDueDate = EnsureUtc(parsedDate);
        if (args.TryGetProperty("status", out var s) && Enum.TryParse<AppConstants.RecurringStatus>(s.GetString(), true, out var status))
            existing.Status = status;

        var result = await _recurringService.UpdateAsync(userId, existing);
        return ($"Updated recurring payment: {result.Name} — {result.Amount:N0}", result);
    }

    public async Task<(string, object?)> DeleteAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolveRecurringAsync(userId, args);
        if (existing is null)
            return ("Recurring payment not found. Try 'list recurring' to see your payments.", null);

        var result = await _recurringService.DeleteAsync(userId, existing.RecurringId);
        return result
            ? ($"Deleted recurring payment: {existing.Name}", true)
            : ("Failed to delete recurring payment.", false);
    }

    public async Task<(string, object?)> MarkAsPaidAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolveRecurringAsync(userId, args);
        if (existing is null)
            return ("Recurring payment not found. Try 'list recurring' to see your payments.", null);

        var result = await _recurringService.MarkAsPaidAsync(userId, existing.RecurringId);
        return result is not null
            ? ($"Marked as paid: {result.Name}. Next due: {result.NextDueDate:yyyy-MM-dd}", result)
            : ("Failed to mark as paid.", null);
    }

    public async Task<(string, object?)> AcknowledgePaidAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolveRecurringAsync(userId, args);
        if (existing is null)
            return ("Recurring payment not found. Try 'list recurring' to see your payments.", null);

        var result = await _recurringService.AcknowledgePaidAsync(userId, existing.RecurringId);
        return result is not null
            ? ($"Acknowledged paid (missed cleared): {result.Name}. Next due: {result.NextDueDate:yyyy-MM-dd}", result)
            : ("Failed to acknowledge payment.", null);
    }

    private async Task<RecurringPayment?> ResolveRecurringAsync(Guid userId, JsonElement args)
    {
        var recurringId = TryStr(args, "recurring_id");
        if (!string.IsNullOrWhiteSpace(recurringId))
            return await _recurringService.GetByIdAsync(userId, recurringId);

        var name = TryStr(args, "name") ?? TryStr(args, "match_name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var all = await _recurringService.GetAllAsync(userId);
        return all.FirstOrDefault(r => r.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<string> ResolveCategoryIdAsync(Guid userId, JsonElement args)
    {
        var categoryId = TryStr(args, "category_id");
        if (!string.IsNullOrWhiteSpace(categoryId)) return categoryId;

        var categoryName = TryStr(args, "category");
        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            var categories = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest
            {
                Keyword = categoryName,
                PageSize = 1
            });
            if (categories.Items.Count > 0)
                return categories.Items[0].CategoryId.ToString();
        }

        var fallback = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest
        {
            Type = AppConstants.TransactionType.Expense,
            PageSize = 1
        });
        return fallback.Items.Count > 0 ? fallback.Items[0].CategoryId.ToString() : "";
    }

    private static string? TryStr(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static decimal TryDecimal(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) && v.TryGetDecimal(out var d) ? d : 0;

    private static DateTime EnsureUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => dt,
        DateTimeKind.Local => dt.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
    };
}
