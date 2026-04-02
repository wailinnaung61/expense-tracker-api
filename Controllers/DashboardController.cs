using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : BaseController
{
    private readonly IDashboardService _service;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(
        IDashboardService service,
        ILogger<DashboardController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// GET /api/dashboard?month=2026-05
    /// Returns all home-screen data in a single request.
    /// Month defaults to the current month when omitted.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> GetDashboard([FromQuery] string? month = null)
    {
        if (UserId is null) return Unauthorized();

        var targetMonth = month ?? DateTime.UtcNow.ToString("yyyy-MM");

        _logger.LogInformation("Getting dashboard for user {UserId}, month {Month}", UserId, targetMonth);

        var result = await _service.GetDashboardAsync(UserId.Value, targetMonth);
        return Ok(result);
    }
}
