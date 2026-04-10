using System.Text.Json;

namespace expense_tracker_backend.Application.Services.Chat.Handlers;

public interface IChatFunctionHandler
{
    Task<(string Summary, object? Data)> HandleAsync(Guid userId, JsonElement args);
}
