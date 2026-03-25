using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;

namespace Infrastructure.Data.Seed;

public static class DefaultTransactionSeeder
{
    public static List<Transaction> GetDefaultTransactions(string userId, List<ExpenseCategory> categories, DateTime now) =>
    [
        // EXPENSE TRANSACTIONS (5)
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Expense,
            CategoryId = FindCategoryId(categories, "Food & Dining"),
            Amount = 15.50m,
            Description = "Lunch at restaurant",
            Merchant = "Local Restaurant",
            PaymentMethod = "Credit Card",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-1).ToString("yyyy-MM-dd"),
            Notes = "Business lunch",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Expense,
            CategoryId = FindCategoryId(categories, "Transportation"),
            Amount = 35.00m,
            Description = "Gas refill",
            Merchant = "Gas Station",
            PaymentMethod = "Debit Card",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-2).ToString("yyyy-MM-dd"),
            Notes = "",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Expense,
            CategoryId = FindCategoryId(categories, "Shopping"),
            Amount = 59.99m,
            Description = "New shoes",
            Merchant = "Shoe Store",
            PaymentMethod = "Credit Card",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-3).ToString("yyyy-MM-dd"),
            Notes = "",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Expense,
            CategoryId = FindCategoryId(categories, "Entertainment"),
            Amount = 12.99m,
            Description = "Movie tickets",
            Merchant = "Cinema",
            PaymentMethod = "Credit Card",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-4).ToString("yyyy-MM-dd"),
            Notes = "",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Expense,
            CategoryId = FindCategoryId(categories, "Bills & Utilities"),
            Amount = 120.00m,
            Description = "Electricity bill",
            Merchant = "Electric Company",
            PaymentMethod = "Bank Transfer",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-5).ToString("yyyy-MM-dd"),
            Notes = "Monthly bill",
            CreatedAt = now,
            UpdatedAt = now
        },

        // INCOME TRANSACTIONS (5)
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Income,
            CategoryId = FindCategoryId(categories, "Salary"),
            Amount = 3500.00m,
            Description = "Monthly salary",
            Merchant = "Company",
            PaymentMethod = "Bank Transfer",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-1).ToString("yyyy-MM-dd"),
            Notes = "March salary",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Income,
            CategoryId = FindCategoryId(categories, "Freelance"),
            Amount = 500.00m,
            Description = "Website project",
            Merchant = "Client",
            PaymentMethod = "Bank Transfer",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-3).ToString("yyyy-MM-dd"),
            Notes = "Frontend project",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Income,
            CategoryId = FindCategoryId(categories, "Investment"),
            Amount = 150.00m,
            Description = "Stock dividends",
            Merchant = "Broker",
            PaymentMethod = "Bank Transfer",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-5).ToString("yyyy-MM-dd"),
            Notes = "Quarterly dividends",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Income,
            CategoryId = FindCategoryId(categories, "Gift"),
            Amount = 100.00m,
            Description = "Birthday gift",
            Merchant = "",
            PaymentMethod = "Cash",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-7).ToString("yyyy-MM-dd"),
            Notes = "From family",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Income,
            CategoryId = FindCategoryId(categories, "Bonus"),
            Amount = 250.00m,
            Description = "Performance bonus",
            Merchant = "Company",
            PaymentMethod = "Bank Transfer",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-10).ToString("yyyy-MM-dd"),
            Notes = "Q1 bonus",
            CreatedAt = now,
            UpdatedAt = now
        }
    ];

    private static string FindCategoryId(List<ExpenseCategory> categories, string displayName) =>
        categories.First(c => c.DisplayName == displayName).CategoryId;
}
