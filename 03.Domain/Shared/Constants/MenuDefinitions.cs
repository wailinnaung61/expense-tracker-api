using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace expense_tracker_backend.Domain.Shared.Constants;

public static class MenuDefinitions
{
    public static IReadOnlyList<MenuItem> ResolveByRole(string role=AppConstants.Roles.User)
    {
        return role switch
        {
            AppConstants.Roles.Admin => Admin,
            AppConstants.Roles.User => User,
            _ => Guest
        };
    }

    public static readonly IReadOnlyList<MenuItem> Admin =
    [
            new MenuItem("dashboard", "Dashboard", "/dashboard"),
            new MenuItem("categories", "Categories", "/expenseCategory"),
            new MenuItem("budgets", "Budgets", "/budget"),
            new MenuItem("expenses", "Expenses", "/tranaction"),
            new MenuItem("savings", "Savings", "/saving"),
            new MenuItem("investments", "Investments", "/investment"),
            new MenuItem("reports", "Reports", "/report"),
            new MenuItem("users", "Users", "/user"),
            new MenuItem("settings", "Settings", "/setting")
    ];

    public static readonly IReadOnlyList<MenuItem> User =
       [
            new MenuItem("dashboard", "Dashboard", "/dashboard"),
            new MenuItem("categories", "Categories", "/expenseCategory"),
            new MenuItem("budgets", "Budgets", "/budget"),
            new MenuItem("expenses", "Expenses", "/tranaction"),
            new MenuItem("savings", "Savings", "/saving"),
            new MenuItem("investments", "Investments", "/investment"),
            new MenuItem("reports", "Reports", "/report"),
            new MenuItem("settings", "Settings", "/setting")
    ];

    public static readonly IReadOnlyList<MenuItem> Guest =
    [
        new MenuItem("login", "Login", "/login")
    ];
}

public record MenuItem(
    string Key,
    string Label,
    string Path
);