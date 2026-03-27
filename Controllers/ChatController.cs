using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : BaseController
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        IChatService chatService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    /// <summary>
    /// Send a chat message to AI assistant
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Chat([FromBody] ChatRequest request)
    {
        if (UserId is null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "Message is required" });

        _logger.LogInformation("Chat request from user {UserId}: {Message}", UserId, request.Message);

        try
        {
            var response = await _chatService.ChatAsync(UserId.Value, request.Message);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Chat failed for user {UserId}", UserId);
            return StatusCode(500, new { message = "Failed to process chat message. Please try again." });
        }
    }
}
