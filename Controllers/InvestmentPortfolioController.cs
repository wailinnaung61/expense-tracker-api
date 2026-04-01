using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class InvestmentPortfolioController : BaseController
{
    private readonly IInvestmentPortfolioService _service;
    private readonly ILogger<InvestmentPortfolioController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public InvestmentPortfolioController(
        IInvestmentPortfolioService service,
        ILogger<InvestmentPortfolioController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _service = service;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Get all portfolios for the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<InvestmentPortfolioDto>>> GetAll()
    {
        if (UserId is null) return Unauthorized();

        var portfolios = await _service.GetAllAsync(UserId.Value);
        return Ok(portfolios);
    }

    /// <summary>
    /// Get portfolio by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvestmentPortfolioDto>> GetById([FromRoute(Name = "id")] Guid portfolioId)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Getting portfolio by ID: {PortfolioId}", portfolioId);

        var portfolio = await _service.GetByIdAsync(UserId.Value, portfolioId);

        if (portfolio is null)
        {
            _logger.LogWarning("Portfolio not found: {PortfolioId}", portfolioId);
            return NotFound(new { message = _localizer["PortfolioNotFound"].Value });
        }

        _logger.LogInformation("Found portfolio: {PortfolioId}", portfolioId);
        return Ok(portfolio);
    }

    /// <summary>
    /// Create a new portfolio
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<InvestmentPortfolioDto>> Create([FromBody] CreateInvestmentPortfolioRequest request)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Creating portfolio: {Name} for user {UserId}", request.Name, UserId);

        try
        {
            var portfolio = await _service.CreateAsync(UserId.Value, request);

            _logger.LogInformation("Portfolio created successfully: {PortfolioId}", portfolio.PortfolioId);
            return CreatedAtAction(nameof(GetById), new { id = portfolio.PortfolioId }, portfolio);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create portfolio: {Name}", request.Name);
            return ErrorResponse(_localizer["PortfolioCreateFailed"].Value);
        }
    }

    /// <summary>
    /// Update a portfolio
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<InvestmentPortfolioDto>> Update(
        [FromRoute(Name = "id")] Guid portfolioId, [FromBody] UpdateInvestmentPortfolioRequest request)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Updating portfolio: {PortfolioId}", portfolioId);

        try
        {
            var portfolio = await _service.UpdateAsync(UserId.Value, portfolioId, request);

            if (portfolio is null)
            {
                _logger.LogWarning("Portfolio not found for update: {PortfolioId}", portfolioId);
                return NotFound(new { message = _localizer["PortfolioNotFound"].Value });
            }

            _logger.LogInformation("Portfolio updated successfully: {PortfolioId}", portfolioId);
            return Ok(portfolio);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update portfolio: {PortfolioId}", portfolioId);
            return ErrorResponse(_localizer["PortfolioUpdateFailed"].Value);
        }
    }

    /// <summary>
    /// Delete a portfolio
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute(Name = "id")] Guid portfolioId)
    {
        if (UserId is null) return Unauthorized();

        _logger.LogInformation("Deleting portfolio: {PortfolioId}", portfolioId);

        try
        {
            var result = await _service.DeleteAsync(UserId.Value, portfolioId);

            if (!result)
            {
                _logger.LogWarning("Portfolio not found for deletion: {PortfolioId}", portfolioId);
                return NotFound(new { message = _localizer["PortfolioNotFound"].Value });
            }

            _logger.LogInformation("Portfolio deleted successfully: {PortfolioId}", portfolioId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete portfolio: {PortfolioId}", portfolioId);
            return ErrorResponse(_localizer["PortfolioDeleteFailed"].Value);
        }
    }
}
