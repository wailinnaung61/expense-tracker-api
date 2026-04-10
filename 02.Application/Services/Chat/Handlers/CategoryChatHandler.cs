using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services.Chat.Handlers;

public class CategoryChatHandler
{
    private readonly IExpenseCategoryService _categoryService;

    public CategoryChatHandler(IExpenseCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    public async Task<(string, object?)> ListCategoriesAsync(Guid userId, JsonElement args)
    {
        AppConstants.TransactionType? type = null;
        if (args.TryGetProperty("type", out var t))
        {
            if (Enum.TryParse<AppConstants.TransactionType>(t.GetString(), true, out var parsed))
                type = parsed;
        }

        var filter = new CategoryFilterRequest { Type = type, PageSize = 50 };
        var result = await _categoryService.GetCategoriesAsync(userId, filter);

        if (result.Items.Count == 0)
            return ("No categories found.", result);

        var lines = result.Items.Select(c => $"• {c.DisplayName} ({c.Type}) {c.Icon}");
        var summary = $"Categories:\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    public async Task<(string, object?)> CreateCategoryAsync(Guid userId, JsonElement args)
    {
        var name = TryStr(args, "name");
        if (string.IsNullOrWhiteSpace(name))
            return ("Please provide a category name.", null);

        var typeStr = TryStr(args, "type") ?? "Expense";
        if (!Enum.TryParse<AppConstants.TransactionType>(typeStr, true, out var type))
            type = AppConstants.TransactionType.Expense;

        var icon = TryStr(args, "icon") ?? "";
        var color = TryStr(args, "color") ?? "#6366F1";

        var dto = new CreateExpenseCategoryDto(name, type, icon, color);
        var result = await _categoryService.CreateExpenseCategoryAsync(userId, dto);

        return ($"Created category: {result.DisplayName} ({result.Type}) {result.Icon}", result);
    }

    public async Task<(string, object?)> UpdateCategoryAsync(Guid userId, JsonElement args)
    {
        var resolved = await ResolveCategoryAsync(userId, args);
        if (resolved is null)
            return ("Category not found. Try 'list categories' to see available categories.", null);

        var name = TryStr(args, "name") ?? TryStr(args, "new_name") ?? resolved.DisplayName;
        var icon = TryStr(args, "icon") ?? resolved.Icon;
        var color = TryStr(args, "color") ?? resolved.Color;

        var dto = new UpdateExpenseCategoryDto(name, icon, color);
        var result = await _categoryService.UpdateExpenseCategoryAsync(userId, resolved.CategoryId, dto);

        return result is not null
            ? ($"Updated category: {result.DisplayName} {result.Icon}", result)
            : ("Failed to update category.", null);
    }

    public async Task<(string, object?)> DeleteCategoryAsync(Guid userId, JsonElement args)
    {
        var resolved = await ResolveCategoryAsync(userId, args);
        if (resolved is null)
            return ("Category not found. Try 'list categories' to see available categories.", null);

        var result = await _categoryService.DeleteExpenseCategoryAsync(userId, resolved.CategoryId);
        return result
            ? ($"Deleted category: {resolved.DisplayName}", true)
            : ("Category not found or cannot be deleted (may have linked transactions).", false);
    }

    private async Task<ExpenseCategory?> ResolveCategoryAsync(Guid userId, JsonElement args)
    {
        var idStr = TryStr(args, "category_id");
        if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var categoryId))
            return await _categoryService.GetExpenseCategoryByIdAsync(userId, categoryId);

        var name = TryStr(args, "name") ?? TryStr(args, "category") ?? TryStr(args, "match_name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var categories = await _categoryService.GetCategoriesAsync(userId, new CategoryFilterRequest
        {
            Keyword = name,
            PageSize = 1
        });
        return categories.Items.FirstOrDefault();
    }

    private static string? TryStr(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) ? v.GetString() : null;
}
