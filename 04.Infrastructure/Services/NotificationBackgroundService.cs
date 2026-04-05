using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Shared.Constants;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace _04.Infrastructure.Services;

public class NotificationBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(6);

    public NotificationBackgroundService(IServiceProvider serviceProvider, ILogger<NotificationBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification background service started — checking every {Interval}", _checkInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckRecurringPaymentsDueAsync();
                await CheckSavingGoalDeadlinesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in notification background service");
            }
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CheckRecurringPaymentsDueAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var today = DateTime.UtcNow.Date;
        var limit = today.AddDays(3);

        var payments = await db.RecurringPayments.AsNoTracking()
            .Where(p => p.Status == AppConstants.RecurringStatus.Active
                     && p.NextDueDate >= today
                     && p.NextDueDate <= limit)
            .ToListAsync();

        _logger.LogInformation("Found {Count} recurring payments due within 3 days", payments.Count);
        foreach (var p in payments)
        {
            try
            {
                // Skip if already notified today for this payment
                var alreadySent = await db.Notifications.AnyAsync(n =>
                    n.UserId == p.UserId
                    && n.Type == NotificationType.RecurringPaymentDue
                    && n.ReferenceId == p.RecurringId
                    && n.CreatedAt >= today);
                if (alreadySent) continue;

                await notif.NotifyRecurringDueAsync(
                    Guid.Parse(p.UserId), p.Name, p.Amount.ToString("N0"),
                    p.NextDueDate.ToString("yyyy-MM-dd"), p.RecurringId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed notify due for {Id}", p.RecurringId);
            }
        }
    }

    private async Task CheckSavingGoalDeadlinesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notif = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var today = DateTime.UtcNow.Date;
        var todayStr = today.ToString("yyyy-MM-dd");
        var limitStr = today.AddDays(7).ToString("yyyy-MM-dd");

        var goals = await db.SavingGoals.AsNoTracking()
            .Where(g => g.Status == AppConstants.SavingGoalStatus.Active
                     && g.CurrentAmount < g.TargetAmount
                     && g.TargetDate.CompareTo(todayStr) >= 0
                     && g.TargetDate.CompareTo(limitStr) <= 0)
            .ToListAsync();

        _logger.LogInformation("Found {Count} saving goals with deadline within 7 days", goals.Count);
        foreach (var g in goals)
        {
            try
            {
                // Skip if already notified today for this goal
                var alreadySent = await db.Notifications.AnyAsync(n =>
                    n.UserId == g.UserId
                    && n.Type == NotificationType.SavingGoalDeadlineNear
                    && n.ReferenceId == g.SavingGoalId
                    && n.CreatedAt >= today);
                if (alreadySent) continue;

                var deadline = DateOnly.ParseExact(g.TargetDate, "yyyy-MM-dd");
                var daysLeft = (deadline.ToDateTime(TimeOnly.MinValue) - today).Days;
                await notif.NotifySavingGoalDeadlineAsync(
                    Guid.Parse(g.UserId), g.GoalName, daysLeft,
                    g.CurrentAmount.ToString("N0"), g.TargetAmount.ToString("N0"),
                    g.SavingGoalId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed notify deadline for {Id}", g.SavingGoalId);
            }
        }
    }
}
