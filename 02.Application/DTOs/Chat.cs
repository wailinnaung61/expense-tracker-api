using System.Text.Json.Serialization;

namespace expense_tracker_backend.Application.DTOs;

public record ChatRequest(string Message);

public record ChatClientAction(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("startMonth")] string? StartMonth = null,
    [property: JsonPropertyName("endMonth")] string? EndMonth = null);

public record ChatResponse(
    string Message,
    [property: JsonPropertyName("name")]
    string? Name = null,
    [property: JsonPropertyName("refreshTarget")]
    string? RefreshTarget = null,
    [property: JsonPropertyName("functionsCalled")]
    List<FunctionCallResult>? FunctionsCalled = null,
    [property: JsonPropertyName("createdAt")]
    DateTime CreatedAt = default,
    [property: JsonPropertyName("clientAction")]
    ChatClientAction? ClientAction = null);

public record FunctionCallResult(
    string FunctionName,
    [property: JsonPropertyName("result")]
    object? Result = null
);

public record PendingConfirmation(
    string Action,
    List<Guid> TransactionIds,
    string Summary
);

// ── Chat Init (context pre-loading) ──────────────────────────────────────────

public record ChatInitResponse(
    string? UserName,
    string? Currency,
    List<ChatCategoryInfo> Categories,
    List<ChatNotificationInfo> RecentNotifications,
    ChatBudgetInfo? Budget,
    ChatSavingsInfo? Savings
);

public record ChatCategoryInfo(string Id, string Name, string Type, string Icon);
public record ChatNotificationInfo(string Title, string Message, DateTime CreatedAt);
public record ChatBudgetInfo(decimal Total, decimal Spent, decimal Remaining, int UsagePercent);
public record ChatSavingsInfo(decimal TotalSaved, int ActiveGoals);

// ── Context snapshot (internal, used between ChatContextLoader and ChatSystemPromptBuilder) ──

public record ChatContextSnapshot(
    string? UserName,
    string? Email,
    string? Currency,
    decimal DailyLimit,
    string? Role,
    string? Locale,
    List<ChatCategoryInfo> Categories,
    List<ChatNotificationInfo> RecentNotifications,
    ChatBudgetInfo? Budget,
    ChatSavingsInfo? Savings,
    List<ChatRecentTransaction> RecentTransactions
);

public record ChatRecentTransaction(string Type, decimal Amount, string Description, string Date);

// ── Redis-serializable chat message ──────────────────────────────────────────

public record SerializedChatMessage(
    string Role,
    string Content,
    DateTime Timestamp
);
