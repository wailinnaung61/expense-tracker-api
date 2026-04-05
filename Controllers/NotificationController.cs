using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : BaseController
{
    private readonly INotificationService _service;

    public NotificationController(INotificationService service)
    {
        _service = service;
    }

    /// <summary>GET /api/notifications/summary — bell icon: unread count + 5 latest</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<NotificationSummary>> GetSummary()
    {
        if (UserId is null) return Unauthorized();
        var result = await _service.GetSummaryAsync(UserId.Value);
        return Ok(result);
    }

    /// <summary>GET /api/notifications/unread-count — just the badge number</summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<object>> GetUnreadCount()
    {
        if (UserId is null) return Unauthorized();
        var count = await _service.GetUnreadCountAsync(UserId.Value);
        return Ok(new { unreadCount = count });
    }

    /// <summary>GET /api/notifications?isRead=false&pageSize=20&cursor=</summary>
    [HttpGet]
    public async Task<ActionResult<PagedNotificationResult>> GetAll(
        [FromQuery] bool? isRead,
        [FromQuery] int pageSize = 20,
        [FromQuery] DateTime? cursor = null)
    {
        if (UserId is null) return Unauthorized();
        var result = await _service.GetNotificationsAsync(UserId.Value, isRead, pageSize, cursor);
        return Ok(result);
    }

    /// <summary>PATCH /api/notifications/{id}/read</summary>
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead([FromRoute] Guid id)
    {
        if (UserId is null) return Unauthorized();
        await _service.MarkAsReadAsync(UserId.Value, id);
        return NoContent();
    }

    /// <summary>PATCH /api/notifications/read-all</summary>
    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        if (UserId is null) return Unauthorized();
        await _service.MarkAllAsReadAsync(UserId.Value);
        return NoContent();
    }

    /// <summary>DELETE /api/notifications/{id}</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        if (UserId is null) return Unauthorized();
        await _service.DeleteAsync(UserId.Value, id);
        return NoContent();
    }

    /// <summary>DELETE /api/notifications/read — delete all read notifications</summary>
    [HttpDelete("read")]
    public async Task<IActionResult> DeleteAllRead()
    {
        if (UserId is null) return Unauthorized();
        await _service.DeleteAllReadAsync(UserId.Value);
        return NoContent();
    }
}
