using expense_tracker_backend.Application.DTOs;

namespace expense_tracker_backend.Application.Interfaces;

public interface IChatService
{
    Task<ChatResponse> ChatAsync(Guid userId, string message);
}
