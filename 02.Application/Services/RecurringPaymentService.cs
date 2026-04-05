using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.Extensions.Logging;

namespace expense_tracker_backend.Application.Services;

public class RecurringPaymentService : IRecurringPaymentService
{
    private readonly IRecurringPaymentRepository _repository;
    private readonly ITranactionRepository _transactionRepository;
    private readonly IAggregationRepository _aggregationRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RecurringPaymentService> _logger;

    public RecurringPaymentService(
        IRecurringPaymentRepository repository,
        ITranactionRepository transactionRepository,
        IAggregationRepository aggregationRepository,
        INotificationService notificationService,
        ILogger<RecurringPaymentService> logger)
    {
        _repository = repository;
        _transactionRepository = transactionRepository;
        _aggregationRepository = aggregationRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<RecurringPayment?> GetByIdAsync(Guid userId, string recurringId)
    {
        return await _repository.GetByIdAsync(userId, recurringId);
    }

    public async Task<List<RecurringPayment>> GetAllAsync(Guid userId)
    {
        return await _repository.GetAllAsync(userId);
    }

    public async Task<List<RecurringPayment>> GetUpcomingAsync(Guid userId, string startDate, string endDate)
    {
        return await _repository.GetByDateRangeAsync(userId, startDate, endDate);
    }

    public async Task<RecurringPayment> CreateAsync(Guid userId, RecurringPayment payment)
    {
        payment.UserId = userId.ToString();
        payment.CreatedAt = DateTime.UtcNow;
        payment.UpdatedAt = DateTime.UtcNow;
        return await _repository.CreateAsync(payment);
    }

    public async Task<RecurringPayment> UpdateAsync(Guid userId, RecurringPayment payment)
    {
        payment.UserId = userId.ToString();
        payment.UpdatedAt = DateTime.UtcNow;
        return await _repository.UpdateAsync(payment);
    }

    public async Task<bool> DeleteAsync(Guid userId, string recurringId)
    {
        return await _repository.DeleteAsync(userId, recurringId);
    }

    public async Task<RecurringPayment?> MarkAsPaidAsync(Guid userId, string recurringId)
    {
        var payment = await _repository.GetByIdAsync(userId, recurringId);
        if (payment == null) return null;

        payment.LastPaidDate = DateTime.UtcNow;
        payment.MissedCount = 0;
        payment.NextDueDate = CalculateNextDueDate(payment.NextDueDate, payment.Frequency);
        payment.UpdatedAt = DateTime.UtcNow;
        return await _repository.UpdateAsync(payment);
    }

    public async Task<int> ProcessOverduePaymentsAsync()
    {
        var today = DateTime.UtcNow;
        var overduePayments = await _repository.GetOverduePaymentsAsync(today.ToString("yyyy-MM-dd"));
        var processedCount = 0;

        foreach (var payment in overduePayments)
        {
            try
            {
                if (payment.AutoPay)
                {
                    // Auto-pay: create a transaction automatically
                    var transaction = new Transaction
                    {
                        TransactionId = Guid.NewGuid().ToString(),
                        UserId = payment.UserId,
                        Type = AppConstants.TransactionType.Expense,
                        CategoryId = payment.CategoryId,
                        Amount = payment.Amount,
                        Description = $"Auto-pay: {payment.Name}",
                        Status = AppConstants.PaymentStatus.Completed,
                        TransactionDate = payment.NextDueDate.ToString("yyyy-MM-dd"),
                        ImageUrl = string.Empty,
                        Notes = $"Auto-generated from recurring payment: {payment.Name}",
                        CreatedAt = DateTime.UtcNow
                    };

                    await _transactionRepository.CreateAsync(transaction);
                    _ = _aggregationRepository.UpdateRedisCacheAsync(transaction);

                    payment.LastPaidDate = DateTime.UtcNow;
                    payment.MissedCount = 0;

                    _logger.LogInformation(
                        "Auto-paid recurring payment {Name} for user {UserId}, amount {Amount}",
                        payment.Name, payment.UserId, payment.Amount);

                    await _notificationService.NotifyRecurringAutoPaidAsync(
                        Guid.Parse(payment.UserId), payment.Name,
                        payment.Amount.ToString("N0"), payment.RecurringId);
                }
                else
                {
                    // Not auto-pay: just increment missed count
                    payment.MissedCount++;

                    _logger.LogInformation(
                        "Recurring payment {Name} overdue for user {UserId}, missed count: {MissedCount}",
                        payment.Name, payment.UserId, payment.MissedCount);

                    await _notificationService.NotifyRecurringOverdueAsync(
                        Guid.Parse(payment.UserId), payment.Name,
                        payment.MissedCount, payment.RecurringId);
                }

                // Advance to next due date (skip past all missed periods)
                while (payment.NextDueDate < today)
                {
                    payment.NextDueDate = CalculateNextDueDate(payment.NextDueDate, payment.Frequency);
                }

                payment.UpdatedAt = DateTime.UtcNow;
                await _repository.UpdateAsync(payment);
                processedCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process recurring payment {RecurringId}", payment.RecurringId);
            }
        }

        return processedCount;
    }

    private static DateTime CalculateNextDueDate(DateTime currentDueDate, AppConstants.RecurringFrequency frequency)
    {
        var dueDate = DateTime.SpecifyKind(currentDueDate, DateTimeKind.Utc);

        var nextDate = frequency switch
        {
            AppConstants.RecurringFrequency.Daily => dueDate.AddDays(1),
            AppConstants.RecurringFrequency.Weekly => dueDate.AddDays(7),
            AppConstants.RecurringFrequency.Monthly => dueDate.AddMonths(1),
            AppConstants.RecurringFrequency.Yearly => dueDate.AddYears(1),
            _ => dueDate.AddMonths(1)
        };

        return nextDate;
    }
}
