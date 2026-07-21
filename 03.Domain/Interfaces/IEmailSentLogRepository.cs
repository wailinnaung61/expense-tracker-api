using expense_tracker_backend.Domain.Entities;

namespace expense_tracker_backend.Domain.Interfaces;

public interface IEmailSentLogRepository
{
    Task<EmailSentLog> CreateAsync(EmailSentLog log);
    Task UpdateAsync(EmailSentLog log);
    Task<(List<EmailSentLog> Items, int TotalCount)> GetByUserIdAsync(
        Guid userId, string? status, int pageSize, DateTime? cursor);
    Task<List<EmailSentLog>> GetPendingAsync(int take = 100);
    Task<bool> ExistsMilestoneAsync(string userId, string type, string? referenceId, string milestone);
}
