using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Application.Interfaces;

public interface IRecurringPaymentService
{
    Task<RecurringPayment?> GetByIdAsync(Guid userId, string recurringId);
    Task<List<RecurringPayment>> GetAllAsync(Guid userId);
    Task<List<RecurringPayment>> GetUpcomingAsync(Guid userId, string startDate, string endDate);
    Task<RecurringPayment> CreateAsync(Guid userId, RecurringPayment payment);
    Task<RecurringPayment> UpdateAsync(Guid userId, RecurringPayment payment);
    Task<bool> DeleteAsync(Guid userId, string recurringId);
    Task<RecurringPayment?> MarkAsPaidAsync(Guid userId, string recurringId);
    /// <summary>
    /// Clears missed count after an external/manual payment without creating a transaction.
    /// Does not advance NextDueDate when it is already in the future (overdue job may have moved it).
    /// </summary>
    Task<RecurringPayment?> AcknowledgePaidAsync(Guid userId, string recurringId);
    Task<int> ProcessOverduePaymentsAsync();
}
