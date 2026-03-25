using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IRecurringPaymentRepository
{
    Task<RecurringPayment?> GetByIdAsync(Guid userId, string recurringId);
    Task<List<RecurringPayment>> GetAllAsync(Guid userId);
    Task<List<RecurringPayment>> GetByDateRangeAsync(Guid userId, string startDate, string endDate);
    Task<List<RecurringPayment>> GetOverduePaymentsAsync(string beforeDate);
    Task<RecurringPayment> CreateAsync(RecurringPayment payment);
    Task<RecurringPayment> UpdateAsync(RecurringPayment payment);
    Task<bool> DeleteAsync(Guid userId, string recurringId);
}
