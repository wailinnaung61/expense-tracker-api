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
    Task<int> ProcessOverduePaymentsAsync();
}
