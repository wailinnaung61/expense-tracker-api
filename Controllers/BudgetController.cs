using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/budgets")]
[Authorize]
public class BudgetController : BaseController
{
    private readonly IBudgetService _service;
    private readonly IBudgetReportService _budgetReportService;
    private readonly ILogger<BudgetController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public BudgetController(
        IBudgetService service,
        IBudgetReportService budgetReportService,
        ILogger<BudgetController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _service = service;
        _budgetReportService = budgetReportService;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// GET /api/budgets/reports/{jobId}/download — pre-signed URL for a budget Excel created via POST .../reports/excel.
    /// </summary>
    [HttpGet("reports/{jobId:guid}/download")]
    public async Task<ActionResult<ExportDownloadResponse>> GetBudgetReportDownload(Guid jobId)
    {
        if (UserId is null) return Unauthorized();

        var result = await _budgetReportService.GetReportDownloadUrlAsync(UserId.Value, jobId);
        return result is null
            ? NotFound(new { message = "Budget report not ready or not found." })
            : Ok(result);
    }

    /// <summary>
    /// POST /api/budgets/{budgetId}/reports/excel — build workbook, upload to S3, return job id for download.
    /// </summary>
    [HttpPost("{budgetId}/reports/excel")]
    public async Task<ActionResult<BudgetReportExcelResponse>> CreateBudgetReportExcel(
        string budgetId, CancellationToken cancellationToken)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Budget Excel report requested for budget {BudgetId} user {UserId}", budgetId, UserId);

        var result = await _budgetReportService.CreateExcelReportAsync(UserId.Value, budgetId, cancellationToken);
        if (result is null)
            return NotFound(new { message = _localizer["BudgetNotFound"].Value });

        return Ok(result);
    }

    /// <summary>
    /// GET /api/budgets/{year}/{month}
    /// Returns budget summary, categories with snapshot, top spending, budget id, and the budget period <c>startDate</c>/<c>endDate</c> (yyyy-MM-dd).
    /// When more than one budget overlaps that calendar month (e.g. pay cycles 15th–15th), this returns the one with the <b>earliest</b> <c>startDate</c>.
    /// For a specific cycle use <see cref="GetContaining"/> or <see cref="GetByRange"/>.
    /// </summary>
    [HttpGet("{year:int}/{month:int}")]
    public async Task<ActionResult<BudgetMonthlyResponse>> GetByMonth(int year, int month)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Getting budget for user {UserId} {Year}/{Month}", UserId, year, month);

        BudgetMonthlyResponse? budget = await _service.GetByMonthAsync(UserId.Value, year, month);

        if (budget is null)
            return NotFound(new { message = _localizer["BudgetNotFound"].Value });

        return Ok(budget);
    }

    /// <summary>
    /// GET /api/budgets/containing?date=2026-05-20
    /// Returns the budget whose inclusive period contains <paramref name="date"/> (yyyy-MM-dd)—the right choice for split pay cycles in one calendar month.
    /// </summary>
    [HttpGet("containing")]
    public async Task<ActionResult<BudgetMonthlyResponse>> GetContaining([FromQuery] string date)
    {
        if (UserId is null) return Unauthorized();

        if (!DateOnly.TryParse(date, out _))
            return BadRequest(new { message = _localizer["DashboardInvalidDateFormat"].Value });

        _logger.LogInformation("Getting budget containing {Date} for user {UserId}", date, UserId);

        var budget = await _service.GetByContainingDateAsync(UserId.Value, date);
        if (budget is null)
            return NotFound(new { message = _localizer["BudgetNotFound"].Value });

        return Ok(budget);
    }

    /// <summary>
    /// GET /api/budgets/range?startDate=2026-05-16&amp;endDate=2026-06-15
    /// Returns merged budget view for every budget overlapping that inclusive range (same semantics as the custom dashboard budget block).
    /// </summary>
    [HttpGet("range")]
    public async Task<ActionResult<BudgetMonthlyResponse>> GetByRange(
        [FromQuery] string startDate,
        [FromQuery] string endDate)
    {
        if (UserId is null) return Unauthorized();

        if (!DateOnly.TryParse(startDate, out var start) || !DateOnly.TryParse(endDate, out var end))
            return BadRequest(new { message = _localizer["DashboardInvalidDateFormat"].Value });

        if (start > end)
            return BadRequest(new { message = _localizer["DashboardStartAfterEnd"].Value });

        _logger.LogInformation(
            "Getting budget by range {StartDate}–{EndDate} for user {UserId}", startDate, endDate, UserId);

        var budget = await _service.GetByDateRangeAsync(UserId.Value, startDate, endDate);
        if (budget is null)
            return NotFound(new { message = _localizer["BudgetNotFound"].Value });

        return Ok(budget);
    }

    /// <summary>
    /// POST /api/budgets
    /// Create a budget: either <see cref="CreateBudgetRequest.Year"/>/<see cref="CreateBudgetRequest.Month"/> (full calendar month)
    /// or <see cref="CreateBudgetRequest.StartDate"/> and <see cref="CreateBudgetRequest.EndDate"/> (custom inclusive range).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BudgetDto>> Create([FromBody] CreateBudgetRequest request)
    {
        if (UserId is null) return Unauthorized();

        var (routeYear, routeMonth) = request.StartDate is not null && request.EndDate is not null
            ? (request.StartDate.Value.Year, request.StartDate.Value.Month)
            : (request.Year, request.Month);

        _logger.LogInformation(
            "Creating budget for user {UserId} {Year}/{Month} amount={Amount} customRange={Custom}",
            UserId, routeYear, routeMonth, request.TotalAmount,
            request.StartDate is not null && request.EndDate is not null);

        try
        {
            var created = await _service.CreateBudgetAsync(UserId.Value, request);
            return CreatedAtAction(nameof(GetByMonth), new { year = routeYear, month = routeMonth }, created);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Budget create validation failed for user {UserId}", UserId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create budget for user {UserId}", UserId);
            return ErrorResponse(_localizer["BudgetCreateFailed"].Value);
        }
    }

    /// <summary>
    /// PUT /api/budgets/{budgetId}
    /// Update total amount, icon, or color
    /// </summary>
    [HttpPut("{budgetId}")]
    public async Task<ActionResult<BudgetDto>> Update(string budgetId, [FromBody] UpdateBudgetRequest request)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Updating budget {BudgetId} for user {UserId}", budgetId, UserId);

        var updated = await _service.UpdateBudgetAsync(UserId.Value, budgetId, request);

        if (updated is null)
            return NotFound(new { message = _localizer["BudgetUpdateFailed"].Value });

        return Ok(updated);
    }

    /// <summary>
    /// POST /api/budgets/{budgetId}/categories
    /// Add a new category to an existing budget at any time
    /// </summary>
    [HttpPost("{budgetId}/categories")]
    public async Task<ActionResult<BudgetCategoryDto>> AddCategory(
        string budgetId, [FromBody] CreateBudgetCategoryRequest request)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Adding category {CategoryId} to budget {BudgetId} for user {UserId}",
            request.CategoryId, budgetId, UserId);

        var result = await _service.AddCategoryAsync(UserId.Value, budgetId, request);

        if (result is null)
            return BadRequest(new { message = _localizer["BudgetCategoryAddFailed"].Value });

        return Ok(result);
    }

    /// <summary>
    /// PUT /api/budget-categories/{budgetCategoryId}
    /// Update allocated amount or alert threshold for a category
    /// </summary>
    [HttpPut("/api/budget-categories/{budgetCategoryId}")]
    public async Task<ActionResult<BudgetCategoryDto>> UpdateCategory(
        string budgetCategoryId, [FromBody] UpdateBudgetCategoryRequest request)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Updating budget category {BudgetCategoryId} for user {UserId}",
            budgetCategoryId, UserId);

        var updated = await _service.UpdateCategoryAllocationAsync(UserId.Value, budgetCategoryId, request);

        if (updated is null)
            return NotFound(new { message = _localizer["BudgetCategoryNotFound"].Value });

        return Ok(updated);
    }

    /// <summary>
    /// DELETE /api/budget-categories/{budgetCategoryId}
    /// Remove a category from a budget
    /// </summary>
    [HttpDelete("/api/budget-categories/{budgetCategoryId}")]
    public async Task<ActionResult> RemoveCategory(string budgetCategoryId)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Removing budget category {BudgetCategoryId} for user {UserId}",
            budgetCategoryId, UserId);

        var deleted = await _service.RemoveCategoryAsync(UserId.Value, budgetCategoryId);

        if (!deleted)
            return NotFound(new { message = _localizer["BudgetCategoryNotFound"].Value });

        return SuccessResponse(_localizer["BudgetCategoryDeleteSuccess"].Value);
    }

    /// <summary>
    /// POST /api/budgets/{budgetId}/reset
    /// Reset all snapshots to zero (start fresh)
    /// </summary>
    [HttpPost("{budgetId}/reset")]
    public async Task<ActionResult> Reset(string budgetId)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Resetting budget {BudgetId} for user {UserId}", budgetId, UserId);

        var success = await _service.ResetBudgetAsync(UserId.Value, budgetId);

        if (!success)
            return NotFound(new { message = _localizer["BudgetResetFailed"].Value });

        return SuccessResponse(_localizer["BudgetResetSuccess"].Value);
    }

    /// <summary>
    /// DELETE /api/budgets/{budgetId}
    /// </summary>
    [HttpDelete("{budgetId}")]
    public async Task<ActionResult> Delete(string budgetId)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Deleting budget {BudgetId} for user {UserId}", budgetId, UserId);

        var deleted = await _service.DeleteBudgetAsync(UserId.Value, budgetId);

        if (!deleted)
            return NotFound(new { message = _localizer["BudgetDeleteFailed"].Value });

        return SuccessResponse(_localizer["BudgetDeleteSuccess"].Value);
    }
}
