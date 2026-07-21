using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace _04.Infrastructure.Services;

public class EmailSentLogRepository : IEmailSentLogRepository
{
    private readonly ApplicationDbContext _context;

    public EmailSentLogRepository(ApplicationDbContext context) => _context = context;

    public async Task<EmailSentLog> CreateAsync(EmailSentLog log)
    {
        _context.EmailSentLogs.Add(log);
        await _context.SaveChangesAsync();
        return log;
    }

    public async Task UpdateAsync(EmailSentLog log)
    {
        _context.EmailSentLogs.Update(log);
        await _context.SaveChangesAsync();
    }

    public async Task<(List<EmailSentLog> Items, int TotalCount)> GetByUserIdAsync(
        Guid userId, string? status, int pageSize, DateTime? cursor)
    {
        var uid = userId.ToString();
        var query = _context.EmailSentLogs.Where(e => e.UserId == uid);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(e => e.Status == status);

        var totalCount = await query.CountAsync();

        if (cursor.HasValue)
            query = query.Where(e => e.CreatedAt < cursor.Value);

        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<List<EmailSentLog>> GetPendingAsync(int take = 100)
    {
        return await _context.EmailSentLogs
            .Where(e => e.Status == EmailSentStatus.Pending)
            .OrderBy(e => e.CreatedAt)
            .Take(take)
            .ToListAsync();
    }

    public async Task<bool> ExistsMilestoneAsync(string userId, string type, string? referenceId, string milestone)
    {
        return await _context.EmailSentLogs.AnyAsync(e =>
            e.UserId == userId
            && e.Type == type
            && e.ReferenceId == referenceId
            && e.Milestone == milestone
            && (e.Status == EmailSentStatus.Sent
                || e.Status == EmailSentStatus.Pending
                || e.Status == EmailSentStatus.Skipped));
    }
}
