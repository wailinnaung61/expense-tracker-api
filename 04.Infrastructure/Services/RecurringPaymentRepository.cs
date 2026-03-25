using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace _04.Infrastructure.Services;

public class RecurringPaymentRepository : IRecurringPaymentRepository
{
    private readonly ApplicationDbContext _context;

    public RecurringPaymentRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RecurringPayment?> GetByIdAsync(Guid userId, string recurringId)
    {
        return await _context.RecurringPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RecurringId == recurringId
                                   && r.UserId == userId.ToString());
    }

    public async Task<List<RecurringPayment>> GetAllAsync(Guid userId)
    {
        return await _context.RecurringPayments
            .AsNoTracking()
            .Where(r => r.UserId == userId.ToString())
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    public async Task<List<RecurringPayment>> GetByDateRangeAsync(Guid userId, string startDate, string endDate)
    {
        var start = DateTime.Parse(startDate).ToUniversalTime();
        var end = DateTime.Parse(endDate).ToUniversalTime();

        return await _context.RecurringPayments
            .AsNoTracking()
            .Where(r => r.UserId == userId.ToString()
                      && r.NextDueDate >= start
                      && r.NextDueDate <= end)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    public async Task<List<RecurringPayment>> GetOverduePaymentsAsync(string beforeDate)
    {
        var date = DateTime.Parse(beforeDate).ToUniversalTime();

        return await _context.RecurringPayments
            .AsNoTracking()
            .Where(r => r.NextDueDate < date
                      && r.Status == AppConstants.RecurringStatus.Active)
            .OrderBy(r => r.NextDueDate)
            .ToListAsync();
    }

    public async Task<RecurringPayment> CreateAsync(RecurringPayment payment)
    {
        payment.CreatedAt = DateTime.UtcNow;
        await _context.RecurringPayments.AddAsync(payment);
        await _context.SaveChangesAsync();
        return payment;
    }

    public async Task<RecurringPayment> UpdateAsync(RecurringPayment payment)
    {
        var existing = await _context.RecurringPayments
            .FirstOrDefaultAsync(r => r.RecurringId == payment.RecurringId
                                   && r.UserId == payment.UserId);

        if (existing == null)
            return null!;

        existing.Name = payment.Name;
        existing.Amount = payment.Amount;
        existing.CategoryId = payment.CategoryId;
        existing.Frequency = payment.Frequency;
        existing.NextDueDate = payment.NextDueDate;
        existing.LastPaidDate = payment.LastPaidDate;
        existing.MissedCount = payment.MissedCount;
        existing.Status = payment.Status;
        existing.AutoPay = payment.AutoPay;
        existing.UpdatedAt = DateTime.UtcNow;

        _context.RecurringPayments.Update(existing);
        await _context.SaveChangesAsync();

        return existing;
    }

    public async Task<bool> DeleteAsync(Guid userId, string recurringId)
    {
        var payment = await _context.RecurringPayments
            .FirstOrDefaultAsync(r => r.RecurringId == recurringId
                                   && r.UserId == userId.ToString());

        if (payment == null)
            return false;

        _context.RecurringPayments.Remove(payment);
        await _context.SaveChangesAsync();

        return true;
    }
}
