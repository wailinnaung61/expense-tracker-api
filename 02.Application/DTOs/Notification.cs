namespace expense_tracker_backend.Application.DTOs;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Message,
    string? ReferenceId,
    string? ReferenceType,
    bool IsRead,
    DateTime CreatedAt,
    DateTime? ReadAt
);

public record NotificationSummary(
    int UnreadCount,
    List<NotificationDto> RecentNotifications
);

public record PagedNotificationResult(
    List<NotificationDto> Items,
    int TotalCount,
    bool HasNextPage,
    DateTime? NextCursor
);
