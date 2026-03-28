using expense_tracker_backend.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace _04.Infrastructure.Services;

public class RecurringPaymentBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RecurringPaymentBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

    public RecurringPaymentBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RecurringPaymentBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RecurringPayment background service started — checking every {Interval}", _checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOverduePaymentsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing overdue recurring payments");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("RecurringPayment background service stopped");
    }

    private async Task ProcessOverduePaymentsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRecurringPaymentService>();

        _logger.LogInformation("Checking for overdue recurring payments...");
        var processedCount = await service.ProcessOverduePaymentsAsync();

        if (processedCount > 0)
        {
            _logger.LogInformation("Processed {Count} overdue recurring payment(s)", processedCount);
        }
        else
        {
            _logger.LogInformation("No overdue recurring payments found");
        }
    }
}
