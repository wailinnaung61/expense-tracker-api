namespace expense_tracker_backend.Domain.Shared.Constants;

public static class MenuDefinitions
{
    public static IReadOnlyList<MenuItem> ResolveByRole(string role, Func<string, string> localize)
    {
        var keys = role switch
        {
            AppConstants.Roles.Admin => AdminKeys,
            AppConstants.Roles.User => UserKeys,
            _ => GuestKeys
        };

        return keys.Select(k => new MenuItem(k.Key, localize($"Menu_{k.Key}"), k.Path)).ToList();
    }

    private static readonly IReadOnlyList<MenuKey> AdminKeys =
    [
        new("dashboard", "/dashboard"),
        new("categories", "/expenseCategory"),
        new("budgets", "/budget"),
        new("expenses", "/tranaction"),
        new("savings", "/saving"),
        new("investments", "/investment"),
        new("reports", "/report"),
        new("emailSent", "/email-sent"),
        new("users", "/user"),
        new("settings", "/setting")
    ];

    private static readonly IReadOnlyList<MenuKey> UserKeys =
    [
        new("dashboard", "/dashboard"),
        new("categories", "/expenseCategory"),
        new("budgets", "/budget"),
        new("expenses", "/tranaction"),
        new("savings", "/saving"),
        new("investments", "/investment"),
        new("reports", "/report"),
        new("emailSent", "/email-sent"),
        new("settings", "/setting")
    ];

    private static readonly IReadOnlyList<MenuKey> GuestKeys =
    [
        new("login", "/login")
    ];

    private record MenuKey(string Key, string Path);
}

public record MenuItem(
    string Key,
    string Label,
    string Path
);