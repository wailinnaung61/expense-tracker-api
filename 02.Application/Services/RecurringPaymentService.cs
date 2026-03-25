using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;

namespace expense_tracker_backend.Application.Services;

public class RecurringPaymentService : IRecurringPaymentService
{
    private readonly IRecurringPaymentRepository _repository;

    public RecurringPaymentService(IRecurringPaymentRepository repository)
    {
        _repository = repository;
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
        // TODO: Fix after entity migration
        throw new NotImplementedException("Recurring payment creation temporarily disabled during PostgreSQL migration");
        // payment.UserId = userId.ToString();
        // payment.CreatedAt = DateTime.UtcNow;
        // payment.UpdatedAt = DateTime.UtcNow;
        // return await _repository.CreateAsync(payment);
    }

    public async Task<RecurringPayment> UpdateAsync(Guid userId, RecurringPayment payment)
    {
        // TODO: Fix after entity migration
        throw new NotImplementedException("Recurring payment update temporarily disabled during PostgreSQL migration");
        // payment.UserId = userId.ToString();
        // return await _repository.UpdateAsync(payment);
    }

    public async Task<bool> DeleteAsync(Guid userId, string recurringId)
    {
        return await _repository.DeleteAsync(userId, recurringId);
    }

    public async Task<RecurringPayment?> MarkAsPaidAsync(Guid userId, string recurringId)
    {
        // TODO: Fix after entity migration
        throw new NotImplementedException("Mark as paid temporarily disabled during PostgreSQL migration");
        // var payment = await _repository.GetByIdAsync(userId, recurringId);
        // if (payment == null) return null;
        // payment.LastPaidDate = DateTime.UtcNow;
        // payment.MissedCount = 0;
        // payment.NextDueDate = CalculateNextDueDate(payment.NextDueDate, payment.Frequency);
        // return await _repository.UpdateAsync(payment);
    }

    public async Task<int> ProcessOverduePaymentsAsync()
    {
        // TODO: Fix after entity migration
        throw new NotImplementedException("Process overdue temporarily disabled during PostgreSQL migration");
        // var today = DateTime.UtcNow;
        // var overduePayments = await _repository.GetOverduePaymentsAsync(today.ToString("yyyy-MM-dd"));
        // var processedCount = 0;
        // foreach (var payment in overduePayments)
        // {
        //     if (payment.Status == AppConstants.RecurringStatus.Paused)
        //         continue;
        //     payment.MissedCount++;
        //     payment.NextDueDate = CalculateNextDueDate(payment.NextDueDate, payment.Frequency);
        //     await _repository.UpdateAsync(payment);
        //     processedCount++;
        // }
        // return processedCount;
    }

    private static DateTime CalculateNextDueDate(DateTime currentDueDate, AppConstants.RecurringFrequency frequency)
    {
        var dueDate = currentDueDate;

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
