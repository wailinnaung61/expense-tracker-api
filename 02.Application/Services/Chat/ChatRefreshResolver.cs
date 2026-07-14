using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services.Chat;

public class ChatRefreshResolver
{
    private static readonly Dictionary<string, string> MutationRefreshMap = new()
    {
        ["add_expense"] = AppConstants.ChatRefreshTarget.Transactions,
        ["add_income"] = AppConstants.ChatRefreshTarget.Transactions,
        ["add_investment"] = AppConstants.ChatRefreshTarget.Transactions,
        ["add_savings"] = AppConstants.ChatRefreshTarget.Transactions,
        ["update_transaction"] = AppConstants.ChatRefreshTarget.Transactions,
        ["delete_transaction"] = AppConstants.ChatRefreshTarget.Transactions,
        ["find_and_delete_transaction"] = AppConstants.ChatRefreshTarget.Transactions,

        ["create_category"] = AppConstants.ChatRefreshTarget.Categories,
        ["update_category"] = AppConstants.ChatRefreshTarget.Categories,
        ["delete_category"] = AppConstants.ChatRefreshTarget.Categories,

        ["create_budget"] = AppConstants.ChatRefreshTarget.Budget,
        ["update_budget"] = AppConstants.ChatRefreshTarget.Budget,
        ["delete_budget"] = AppConstants.ChatRefreshTarget.Budget,
        ["add_budget_category"] = AppConstants.ChatRefreshTarget.Budget,
        ["remove_budget_category"] = AppConstants.ChatRefreshTarget.Budget,

        ["create_recurring_payment"] = AppConstants.ChatRefreshTarget.RecurringPayments,
        ["update_recurring_payment"] = AppConstants.ChatRefreshTarget.RecurringPayments,
        ["delete_recurring_payment"] = AppConstants.ChatRefreshTarget.RecurringPayments,
        ["mark_recurring_paid"] = AppConstants.ChatRefreshTarget.RecurringPayments,
        ["acknowledge_recurring_paid"] = AppConstants.ChatRefreshTarget.RecurringPayments,

        ["create_saving_goal"] = AppConstants.ChatRefreshTarget.Savings,
        ["update_saving_goal"] = AppConstants.ChatRefreshTarget.Savings,
        ["delete_saving_goal"] = AppConstants.ChatRefreshTarget.Savings,
        ["add_saving_contribution"] = AppConstants.ChatRefreshTarget.Savings,

        ["create_portfolio"] = AppConstants.ChatRefreshTarget.Investments,
        ["update_portfolio"] = AppConstants.ChatRefreshTarget.Investments,
        ["delete_portfolio"] = AppConstants.ChatRefreshTarget.Investments,
        ["create_investment_record"] = AppConstants.ChatRefreshTarget.Investments,
        ["update_investment"] = AppConstants.ChatRefreshTarget.Investments,
        ["delete_investment"] = AppConstants.ChatRefreshTarget.Investments,
    };

    public List<string> ResolveAll(IEnumerable<FunctionCallResult> functionsCalled)
    {
        return functionsCalled
            .Select(f => f.FunctionName)
            .Where(MutationRefreshMap.ContainsKey)
            .Select(n => MutationRefreshMap[n])
            .Distinct()
            .ToList();
    }

    public string? Resolve(IEnumerable<FunctionCallResult> functionsCalled)
    {
        foreach (var fn in functionsCalled)
        {
            if (MutationRefreshMap.TryGetValue(fn.FunctionName, out var target))
                return target;
        }
        return null;
    }

    public bool IsMutation(string functionName) => MutationRefreshMap.ContainsKey(functionName);
}
