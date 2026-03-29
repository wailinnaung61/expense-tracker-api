using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;

namespace Infrastructure.Data.Seed;

public static class DefaultCategorySeeder
{
    public static List<ExpenseCategory> GetDefaultCategories(string userId, DateTime now) =>
    [
        // EXPENSE CATEGORIES (5)
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Food & Dining",
            Type = AppConstants.TransactionType.Expense,
            Icon = "🍔",
            Color = "#dc2626",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Transportation",
            Type = AppConstants.TransactionType.Expense,
            Icon = "🚗",
            Color = "#0891b2",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Shopping",
            Type = AppConstants.TransactionType.Expense,
            Icon = "🛍️",
            Color = "#ca8a04",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Entertainment",
            Type = AppConstants.TransactionType.Expense,
            Icon = "🎬",
            Color = "#9333ea",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Bills & Utilities",
            Type = AppConstants.TransactionType.Expense,
            Icon = "💡",
            Color = "#d97706",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },

        // INCOME CATEGORIES (5)
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Salary",
            Type = AppConstants.TransactionType.Income,
            Icon = "💰",
            Color = "#16a34a",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Freelance",
            Type = AppConstants.TransactionType.Income,
            Icon = "💼",
            Color = "#0d9488",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Investment",
            Type = AppConstants.TransactionType.Income,
            Icon = "📈",
            Color = "#65a30d",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Gift",
            Type = AppConstants.TransactionType.Income,
            Icon = "🎁",
            Color = "#e11d48",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Bonus",
            Type = AppConstants.TransactionType.Income,
            Icon = "🎉",
            Color = "#ea580c",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },

        // SAVING GOAL CATEGORIES (6)
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Emergency Fund",
            Type = AppConstants.TransactionType.SavingGoal,
            Icon = "🛟",
            Color = "#0ea5e9",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Vacation",
            Type = AppConstants.TransactionType.SavingGoal,
            Icon = "🏖️",
            Color = "#06b6d4",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Vehicle",
            Type = AppConstants.TransactionType.SavingGoal,
            Icon = "🚙",
            Color = "#3b82f6",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Home",
            Type = AppConstants.TransactionType.SavingGoal,
            Icon = "🏠",
            Color = "#2563eb",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Education",
            Type = AppConstants.TransactionType.SavingGoal,
            Icon = "🎓",
            Color = "#8b5cf6",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            CategoryId = Guid.NewGuid().ToString(),
            UserId = userId,
            DisplayName = "Retirement",
            Type = AppConstants.TransactionType.SavingGoal,
            Icon = "🧓",
            Color = "#6366f1",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        }
    ];
}
