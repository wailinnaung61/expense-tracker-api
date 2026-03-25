using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AggregationController : BaseController
{
    private readonly IAggregationService _aggregationService;
    private readonly ILogger<AggregationController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public AggregationController(
        IAggregationService aggregationService,
        ILogger<AggregationController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _aggregationService = aggregationService;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Get daily aggregation for a specific date
    /// </summary>
    [HttpGet("daily/{date}")]
    public async Task<ActionResult<DailyAggregation>> GetDailyAggregation([FromRoute] string date)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting daily aggregation for user: {UserId}, date: {Date}", UserId, date);

        var result = await _aggregationService.GetDailyAggregationAsync(UserId.Value, date);
        
        if (result == null)
            return NotFound(new { message = _localizer["AggregationNotFound"].Value });

        return Ok(result);
    }

    /// <summary>
    /// Get daily aggregations for a date range
    /// </summary>
    [HttpGet("daily")]
    public async Task<ActionResult<List<DailyAggregation>>> GetDailyAggregationsRange(
        [FromQuery] string startDate,
        [FromQuery] string endDate)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting daily aggregations for user: {UserId}, range: {StartDate} to {EndDate}", 
            UserId, startDate, endDate);

        var result = await _aggregationService.GetDailyAggregationsRangeAsync(UserId.Value, startDate, endDate);
        return Ok(result);
    }

    /// <summary>
    /// Get weekly aggregation for a specific week
    /// </summary>
    [HttpGet("weekly/{week}")]
    public async Task<ActionResult<WeeklyAggregation>> GetWeeklyAggregation([FromRoute] string week)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting weekly aggregation for user: {UserId}, week: {Week}", UserId, week);

        var result = await _aggregationService.GetWeeklyAggregationAsync(UserId.Value, week);
        
        if (result == null)
            return NotFound(new { message = _localizer["AggregationNotFound"].Value });

        return Ok(result);
    }

    /// <summary>
    /// Get weekly aggregations for a week range
    /// </summary>
    [HttpGet("weekly")]
    public async Task<ActionResult<List<WeeklyAggregation>>> GetWeeklyAggregationsRange(
        [FromQuery] string startWeek,
        [FromQuery] string endWeek)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting weekly aggregations for user: {UserId}, range: {StartWeek} to {EndWeek}", 
            UserId, startWeek, endWeek);

        var result = await _aggregationService.GetWeeklyAggregationsRangeAsync(UserId.Value, startWeek, endWeek);
        return Ok(result);
    }

    /// <summary>
    /// Get monthly aggregation for a specific month
    /// </summary>
    [HttpGet("monthly/{month}")]
    public async Task<ActionResult<MonthlyAggregation>> GetMonthlyAggregation([FromRoute] string month)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting monthly aggregation for user: {UserId}, month: {Month}", UserId, month);

        var result = await _aggregationService.GetMonthlyAggregationAsync(UserId.Value, month);

        if (result == null)
            return NotFound(new { message = _localizer["AggregationNotFound"].Value });

        return Ok(result);
    }

    /// <summary>
    /// Get monthly aggregations for a month range
    /// </summary>
    [HttpGet("monthly")]
    public async Task<ActionResult<List<MonthlyAggregation>>> GetMonthlyAggregationsRange(
        [FromQuery] string startMonth,
        [FromQuery] string endMonth)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting monthly aggregations for user: {UserId}, range: {StartMonth} to {EndMonth}", 
            UserId, startMonth, endMonth);

        var result = await _aggregationService.GetMonthlyAggregationsRangeAsync(UserId.Value, startMonth, endMonth);
        return Ok(result);
    }

    /// <summary>
    /// Get yearly aggregation for a specific year
    /// </summary>
    [HttpGet("yearly/{year}")]
    public async Task<ActionResult<YearlyAggregation>> GetYearlyAggregation([FromRoute] string year)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting yearly aggregation for user: {UserId}, year: {Year}", UserId, year);

        var result = await _aggregationService.GetYearlyAggregationAsync(UserId.Value, year);
        
        if (result == null)
            return NotFound(new { message = _localizer["AggregationNotFound"].Value });

        return Ok(result);
    }

    /// <summary>
    /// Get yearly aggregations for a year range
    /// </summary>
    [HttpGet("yearly")]
    public async Task<ActionResult<List<YearlyAggregation>>> GetYearlyAggregationsRange(
        [FromQuery] string startYear,
        [FromQuery] string endYear)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting yearly aggregations for user: {UserId}, range: {StartYear} to {EndYear}", 
            UserId, startYear, endYear);

        var result = await _aggregationService.GetYearlyAggregationsRangeAsync(UserId.Value, startYear, endYear);
        return Ok(result);
    }

    /// <summary>
    /// Get category monthly aggregations for a specific month
    /// </summary>
    [HttpGet("category/monthly/{month}")]
    public async Task<ActionResult<List<CategoryMonthlyAggregation>>> GetCategoryMonthlyAggregations([FromRoute] string month)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting category monthly aggregations for user: {UserId}, month: {Month}", UserId, month);

        var result = await _aggregationService.GetCategoryMonthlyAggregationsAsync(UserId.Value, month);
        return Ok(result);
    }

    /// <summary>
    /// Get expense breakdown with categories for the current month
    /// </summary>
    [HttpGet("expense-breakdown/{month}")]
    public async Task<ActionResult<ExpenseBreakdown>> GetExpenseBreakdown([FromRoute] string month)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting expense breakdown for user: {UserId}, month: {Month}", UserId, month);

        var result = await _aggregationService.GetExpenseBreakdownAsync(UserId.Value, month);
        return Ok(result);
    }
}
