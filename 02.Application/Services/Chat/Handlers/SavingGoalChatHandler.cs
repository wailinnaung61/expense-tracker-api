using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services.Chat.Handlers;

public class SavingGoalChatHandler
{
    private readonly ISavingGoalService _savingGoalService;

    public SavingGoalChatHandler(ISavingGoalService savingGoalService)
    {
        _savingGoalService = savingGoalService;
    }

    public async Task<(string, object?)> ListGoalsAsync(Guid userId, JsonElement args)
    {
        AppConstants.SavingGoalStatus? status = null;
        if (args.TryGetProperty("status", out var s) && Enum.TryParse<AppConstants.SavingGoalStatus>(s.GetString(), true, out var parsed))
            status = parsed;

        var filter = new SavingGoalFilterRequest { Status = status, PageSize = 20 };
        var result = await _savingGoalService.GetGoalsAsync(userId, filter);

        if (result.Items.Count == 0)
            return ("No saving goals found.", result);

        var lines = result.Items.Select(g =>
            $"• {g.GoalName}: {g.CurrentAmount:N0}/{g.TargetAmount:N0} ({g.ProgressPercentage:F0}%) [{g.Status}]");
        var summary = $"Saving Goals:\n{string.Join("\n", lines)}";

        return (summary, result);
    }

    public async Task<(string, object?)> CreateGoalAsync(Guid userId, JsonElement args)
    {
        var goalName = TryStr(args, "goal_name");
        if (string.IsNullOrWhiteSpace(goalName))
            return ("Please provide a name for the saving goal.", null);

        var targetAmount = TryDecimal(args, "target_amount");
        if (targetAmount <= 0)
            return ("Please provide a target amount.", null);

        var targetDate = TryStr(args, "target_date") ?? DateTime.UtcNow.AddMonths(6).ToString("yyyy-MM-dd");
        var description = TryStr(args, "description") ?? "";

        var goalType = AppConstants.SavingGoalType.Other;
        if (args.TryGetProperty("goal_type", out var gt) && Enum.TryParse<AppConstants.SavingGoalType>(gt.GetString(), true, out var parsedType))
            goalType = parsedType;

        var request = new CreateSavingGoalRequest(goalName, targetAmount, targetDate, description, goalType);
        var result = await _savingGoalService.CreateAsync(userId, request);

        return ($"Created saving goal: {result.GoalName} — Target: {result.TargetAmount:N0} by {result.TargetDate}", result);
    }

    public async Task<(string, object?)> UpdateGoalAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolveGoalAsync(userId, args);
        if (existing is null)
            return ("Saving goal not found. Try 'list saving goals' to see your goals.", null);

        var goalId = existing.SavingGoalId;
        var goalName = TryStr(args, "goal_name") ?? TryStr(args, "new_name") ?? existing.GoalName;
        var targetAmount = TryDecimal(args, "target_amount");
        if (targetAmount <= 0) targetAmount = existing.TargetAmount;
        var targetDate = TryStr(args, "target_date") ?? existing.TargetDate;

        var status = AppConstants.SavingGoalStatus.Active;
        if (args.TryGetProperty("status", out var s) && Enum.TryParse<AppConstants.SavingGoalStatus>(s.GetString(), true, out var parsedStatus))
            status = parsedStatus;
        else if (Enum.TryParse<AppConstants.SavingGoalStatus>(existing.Status, true, out var existingStatus))
            status = existingStatus;

        var request = new UpdateSavingGoalRequest(goalName, targetAmount, targetDate, status);
        var result = await _savingGoalService.UpdateAsync(userId, goalId, request);

        return result is not null
            ? ($"Updated saving goal: {result.GoalName} — Target: {result.TargetAmount:N0}", result)
            : ("Failed to update saving goal.", null);
    }

    public async Task<(string, object?)> DeleteGoalAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolveGoalAsync(userId, args);
        if (existing is null)
            return ("Saving goal not found. Try 'list saving goals' to see your goals.", null);

        var result = await _savingGoalService.DeleteAsync(userId, existing.SavingGoalId);
        return result
            ? ($"Deleted saving goal: {existing.GoalName}", true)
            : ("Saving goal not found.", false);
    }

    public async Task<(string, object?)> AddContributionAsync(Guid userId, JsonElement args)
    {
        var existing = await ResolveGoalAsync(userId, args);
        if (existing is null)
            return ("Saving goal not found. Try 'list saving goals' to see your goals.", null);

        var typeStr = TryStr(args, "type") ?? "Deposit";
        if (!Enum.TryParse<AppConstants.SavingTransactionType>(typeStr, true, out var contribType))
            contribType = AppConstants.SavingTransactionType.Deposit;

        var amount = TryDecimal(args, "amount");
        if (amount <= 0)
            return ("Please provide the contribution amount.", null);

        var date = TryStr(args, "date") ?? DateTime.UtcNow.ToString("yyyy-MM-dd");
        var notes = TryStr(args, "notes") ?? "";

        var request = new AddSavingContributionRequest(contribType, amount, date, notes);
        var result = await _savingGoalService.AddContributionAsync(userId, existing.SavingGoalId, request);

        return ($"{contribType} of {amount:N0} added to {existing.GoalName}.", result);
    }

    public async Task<(string, object?)> GetDashboardAsync(Guid userId)
    {
        var result = await _savingGoalService.GetDashboardAsync(userId);

        var summary = $"Saving Dashboard:\n" +
            $"Total Saved: {result.TotalSaved:N0} / Target: {result.TotalTarget:N0} ({result.OverallProgressPercentage:F0}%)\n" +
            $"Active Goals: {result.ActiveGoalsCount} | Completed: {result.CompletedGoalsCount}";

        if (result.Top5Goals.Count > 0)
        {
            var lines = result.Top5Goals.Select(g =>
                $"• {g.GoalName}: {g.CurrentAmount:N0}/{g.TargetAmount:N0} ({g.ProgressPercentage:F0}%)");
            summary += $"\n\nTop Goals:\n{string.Join("\n", lines)}";
        }

        return (summary, result);
    }

    private async Task<SavingGoalDto?> ResolveGoalAsync(Guid userId, JsonElement args)
    {
        var idStr = TryStr(args, "saving_goal_id");
        if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var goalId))
            return await _savingGoalService.GetByIdAsync(userId, goalId);

        var name = TryStr(args, "goal_name") ?? TryStr(args, "name") ?? TryStr(args, "match_name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var filter = new SavingGoalFilterRequest { Keyword = name, PageSize = 1 };
        var result = await _savingGoalService.GetGoalsAsync(userId, filter);
        return result.Items.FirstOrDefault();
    }

    private static string? TryStr(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) ? v.GetString() : null;

    private static decimal TryDecimal(JsonElement args, string prop) =>
        args.TryGetProperty(prop, out var v) && v.TryGetDecimal(out var d) ? d : 0;
}
