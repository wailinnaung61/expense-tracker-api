using System.Text.Json;
using expense_tracker_backend.Application.DTOs;
using Microsoft.Extensions.Caching.Distributed;
using OpenAI.Chat;

namespace expense_tracker_backend.Application.Services.Chat;

public class ChatHistoryStore
{
    private readonly IDistributedCache _cache;
    private const int MaxHistoryMessages = 10;
    private static readonly DistributedCacheEntryOptions HistoryOptions = new()
    {
        SlidingExpiration = TimeSpan.FromMinutes(30)
    };
    private static readonly DistributedCacheEntryOptions PendingOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
    };

    public ChatHistoryStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    private static string HistoryKey(Guid userId) => $"chat:history:{userId}";
    private static string PendingKey(Guid userId) => $"chat:pending:{userId}";

    public async Task<List<ChatMessage>> GetHistoryAsync(Guid userId)
    {
        var json = await _cache.GetStringAsync(HistoryKey(userId));
        if (string.IsNullOrEmpty(json)) return [];

        var serialized = JsonSerializer.Deserialize<List<SerializedChatMessage>>(json) ?? [];
        return serialized.Select<SerializedChatMessage, ChatMessage>(m => m.Role switch
        {
            "user" => new UserChatMessage(m.Content),
            "assistant" => new AssistantChatMessage(m.Content),
            _ => new UserChatMessage(m.Content)
        }).ToList();
    }

    public async Task SaveHistoryAsync(Guid userId, List<ChatMessage> messages)
    {
        var serialized = messages
            .Where(m => m is UserChatMessage || (m is AssistantChatMessage am && am.ToolCalls.Count == 0))
            .TakeLast(MaxHistoryMessages)
            .Select(m => new SerializedChatMessage(
                m switch { UserChatMessage => "user", AssistantChatMessage => "assistant", _ => "user" },
                m switch
                {
                    UserChatMessage u => u.Content.FirstOrDefault()?.Text ?? "",
                    AssistantChatMessage a => a.Content.FirstOrDefault()?.Text ?? "",
                    _ => ""
                },
                DateTime.UtcNow
            ))
            .ToList();

        var json = JsonSerializer.Serialize(serialized);
        await _cache.SetStringAsync(HistoryKey(userId), json, HistoryOptions);
    }

    public async Task ClearHistoryAsync(Guid userId)
    {
        await _cache.RemoveAsync(HistoryKey(userId));
    }

    public async Task<PendingConfirmation?> GetPendingAsync(Guid userId)
    {
        var json = await _cache.GetStringAsync(PendingKey(userId));
        if (string.IsNullOrEmpty(json)) return null;
        return JsonSerializer.Deserialize<PendingConfirmation>(json);
    }

    public async Task SetPendingAsync(Guid userId, PendingConfirmation pending)
    {
        var json = JsonSerializer.Serialize(pending);
        await _cache.SetStringAsync(PendingKey(userId), json, PendingOptions);
    }

    public async Task RemovePendingAsync(Guid userId)
    {
        await _cache.RemoveAsync(PendingKey(userId));
    }
}
