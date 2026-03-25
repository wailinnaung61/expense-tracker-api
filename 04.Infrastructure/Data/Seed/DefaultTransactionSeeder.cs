using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;

namespace Infrastructure.Data.Seed;

public static class DefaultTransactionSeeder
{
    public static List<Transaction> GetDefaultTransactions(string userId, List<ExpenseCategory> categories, DateTime now) =>
    [
        // EXPENSE TRANSACTIONS
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
            TransactionDate = now.AddDays(-1),
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
            TransactionDate = now.AddDays(-2),
            Notes = "",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Expense,
            CategoryId = FindCategoryId(categories, "Groceries"),
            Amount = 82.30m,
            Description = "Weekly groceries",
            Merchant = "Supermarket",
            PaymentMethod = "Credit Card",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-3),
            Notes = "Weekly shopping",
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
            TransactionDate = now.AddDays(-4),
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
            TransactionDate = now.AddDays(-5),
            Notes = "Monthly bill",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Expense,
            CategoryId = FindCategoryId(categories, "Healthcare"),
            Amount = 45.00m,
            Description = "Pharmacy",
            Merchant = "Pharmacy Store",
            PaymentMethod = "Cash",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-6),
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
            TransactionDate = now.AddDays(-7),
            Notes = "",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Expense,
            CategoryId = FindCategoryId(categories, "Education"),
            Amount = 29.99m,
            Description = "Online course",
            Merchant = "Udemy",
            PaymentMethod = "Credit Card",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-8),
            Notes = "Programming course",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Expense,
            CategoryId = FindCategoryId(categories, "Housing"),
            Amount = 850.00m,
            Description = "Monthly rent",
            Merchant = "Landlord",
            PaymentMethod = "Bank Transfer",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-10),
            Notes = "March rent",
            CreatedAt = now,
            UpdatedAt = now
        },
        new()
        {
            TransactionId = Guid.NewGuid().ToString(),
            UserId = userId,
            Type = AppConstants.TransactionType.Expense,
            CategoryId = FindCategoryId(categories, "Personal Care"),
            Amount = 25.00m,
            Description = "Haircut",
            Merchant = "Barber Shop",
            PaymentMethod = "Cash",
            Status = AppConstants.PaymentStatus.Completed,
            TransactionDate = now.AddDays(-12),
            Notes = "",
            CreatedAt = now,
            UpdatedAt = now
        },

        // INCOME TRANSACTIONS
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
            TransactionDate = now.AddDays(-1),
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
            TransactionDate = now.AddDays(-5),
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
            TransactionDate = now.AddDays(-10),
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
            TransactionDate = now.AddDays(-15),
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
            TransactionDate = now.AddDays(-20),
            Notes = "Q1 bonus",
            CreatedAt = now,
            UpdatedAt = now
        }
    ];

    private static string FindCategoryId(List<ExpenseCategory> categories, string displayName) =>
        categories.First(c => c.DisplayName == displayName).CategoryId;
}
