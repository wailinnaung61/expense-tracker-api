using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : BaseController
{
    private readonly IDashboardService _service;
    private readonly ILogger<DashboardController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public DashboardController(
        IDashboardService service,
        ILogger<DashboardController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _service = service;
        _logger = logger;
        _localizer = localizer;
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

    /// <summary>
    /// GET /api/dashboard/custom?startDate=2026-01-01&endDate=2026-03-31
    /// Returns full dashboard data for a custom date range.
    /// </summary>
    [HttpGet("custom")]
    public async Task<ActionResult<DashboardResponse>> GetDashboardByRange(
        [FromQuery] string startDate,
        [FromQuery] string endDate)
    {
        if (UserId is null) return Unauthorized();

        if (!DateOnly.TryParse(startDate, out var start) || !DateOnly.TryParse(endDate, out var end))
            return BadRequest(new { message = _localizer["DashboardInvalidDateFormat"].Value });

        if (start > end)
            return BadRequest(new { message = _localizer["DashboardStartAfterEnd"].Value });

        if (start.AddMonths(24) < end)
            return BadRequest(new { message = _localizer["DashboardRangeExceeds24Months"].Value });

        _logger.LogInformation("Getting dashboard for user {UserId}, range {StartDate} to {EndDate}", UserId, startDate, endDate);

        var result = await _service.GetDashboardByRangeAsync(UserId.Value, startDate, endDate);
        return Ok(result);
    }
}
