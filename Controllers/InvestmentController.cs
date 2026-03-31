using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvestmentController : BaseController
{
    private readonly IInvestmentService _service;
    private readonly ILogger<InvestmentController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public InvestmentController(
        IInvestmentService service,
        ILogger<InvestmentController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _service = service;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Get investments with filters and cursor-based pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<InvestmentDto>>> GetAll([FromQuery] InvestmentFilterRequest filter)
    {
        if (UserId is null) return Unauthorized();

        var result = await _service.GetInvestmentsAsync(UserId.Value, filter);
        return Ok(result);
    }

    /// <summary>
    /// Get investment by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvestmentDto>> GetById([FromRoute(Name = "id")] Guid investmentId)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Getting investment by ID: {InvestmentId}", investmentId);

        var investment = await _service.GetByIdAsync(UserId.Value, investmentId);

        if (investment is null)
        {
            _logger.LogWarning("Investment not found: {InvestmentId}", investmentId);
            return NotFound(new { message = _localizer["InvestmentNotFound"].Value });
        }

        _logger.LogInformation("Found investment: {InvestmentId}", investmentId);
        return Ok(investment);
    }

    /// <summary>
    /// Get investment dashboard: totals, P&L, allocation, top/worst performers
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<InvestmentDashboardResponse>> GetDashboard()
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Getting investment dashboard for user {UserId}", UserId);

        var dashboard = await _service.GetDashboardAsync(UserId.Value);
        return Ok(dashboard);
    }

    /// <summary>
    /// Create a new investment
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InvestmentDto>> Create([FromBody] CreateInvestmentRequest request)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Creating investment: {AssetName}, Amount: {Amount}, Type: {AssetType}",
            request.AssetName, request.Quantity * request.PurchasePrice, request.AssetType);

        try
        {
            var investment = await _service.CreateAsync(UserId.Value, request);

            _logger.LogInformation("Investment created successfully: {InvestmentId}", investment.InvestmentId);
            return CreatedAtAction(nameof(GetById), new { id = investment.InvestmentId }, investment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create investment: {AssetName}", request.AssetName);
            return ErrorResponse(_localizer["InvestmentCreateFailed"].Value);
        }
    }

    /// <summary>
    /// Update an investment
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvestmentDto>> Update(
        [FromRoute(Name = "id")] Guid investmentId, [FromBody] UpdateInvestmentRequest request)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Updating investment: {InvestmentId}", investmentId);

        try
        {
            var investment = await _service.UpdateAsync(UserId.Value, investmentId, request);

            if (investment is null)
            {
                _logger.LogWarning("Investment not found for update: {InvestmentId}", investmentId);
                return NotFound(new { message = _localizer["InvestmentNotFound"].Value });
            }

            _logger.LogInformation("Investment updated successfully: {InvestmentId}", investmentId);
            return Ok(investment);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update investment: {InvestmentId}", investmentId);
            return ErrorResponse(_localizer["InvestmentUpdateFailed"].Value);
        }
    }

    /// <summary>
    /// Delete an investment
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute(Name = "id")] Guid investmentId)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Deleting investment: {InvestmentId}", investmentId);

        try
        {
            var result = await _service.DeleteAsync(UserId.Value, investmentId);

            if (!result)
            {
                _logger.LogWarning("Investment not found for deletion: {InvestmentId}", investmentId);
                return NotFound(new { message = _localizer["InvestmentNotFound"].Value });
            }

            _logger.LogInformation("Investment deleted successfully: {InvestmentId}", investmentId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete investment: {InvestmentId}", investmentId);
            return ErrorResponse(_localizer["InvestmentDeleteFailed"].Value);
        }
    }
}
