using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TranactionController : BaseController
{
    private readonly ITranactionService _expenseService;
    private readonly ILogger<TranactionController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public TranactionController(
        ITranactionService expenseService,
        ILogger<TranactionController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _expenseService = expenseService;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Get transactions with flexible filtering and pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<Tranaction>>> GetTransactions([FromQuery] TransactionFilterRequest filter)
    {
        if (UserId is null)
            return Unauthorized();

        var result = await _expenseService.GetTransactionsAsync(UserId.Value, filter);
        return Ok(result);
    }

    /// <summary>
    /// Get tranaction by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Tranaction>> GetById([FromRoute(Name = "id")] Guid tranactionId)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting tranaction by ID: {TranactionId}", tranactionId);

        var tranaction = await _expenseService.GetTranactionByIdAsync(UserId.Value, tranactionId);

        if (tranaction is null)
        {
            _logger.LogWarning("Tranaction not found: {TranactionId}", tranactionId);
            return NotFound(new { message = _localizer["TransactionNotFound"].Value });
        }

        _logger.LogInformation("Found tranaction: {TranactionId}", tranactionId);
        return Ok(tranaction);
    }

    /// <summary>
    /// Create a new expense
    /// </summary>
    [HttpPost]
    [Route("create")]
    public async Task<ActionResult<Tranaction>> Create([FromBody] CreateTranactionDto dto)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Creating new expense: {TranactionType}, Amount: {Amount}, Category: {Category}",
            dto.type, dto.Amount, dto.CategoryId);

        try
        {
            var tranaction = await _expenseService.CreateTranactionAsync(dto, UserId.Value);

            _logger.LogInformation("Tranaction created successfully: {TranactionId}", tranaction.TranactionId);
            return CreatedAtAction(nameof(GetById), new { id = tranaction.TranactionId }, tranaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create expense: {TranactionType}, Amount: {Amount}, Category: {Category}",
                dto.type, dto.Amount, dto.CategoryId);
            return ErrorResponse(_localizer["TransactionCreateFailed"].Value);
        }
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Tranaction>> Update([FromRoute(Name = "id")] Guid tranactionId, [FromBody] UpdateTranactionDto dto)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Updating tranaction: {TranactionId},", tranactionId);
        try
        {
            var tranaction = await _expenseService.UpdateTranactionAsync(UserId.Value, tranactionId, dto);

            if (tranaction is null)
            {
                _logger.LogWarning("Tranaction not found for update: {TranactionId}", tranactionId);
                return NotFound(new { message = _localizer["TransactionNotFound"].Value });
            }

            _logger.LogInformation("Tranaction updated successfully: {TranactionId}", tranactionId);
            return Ok(tranaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tranaction: {TranactionId}", tranactionId);
            return ErrorResponse(_localizer["TransactionUpdateFailed"].Value);
        }
    }

    /// <summary>
    /// Delete an expense
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute(Name = "id")] Guid tranactionId)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Deleting tranaction: {TranactionId}", tranactionId);

        try
        {
            var result = await _expenseService.DeleteTranactionAsync(UserId.Value, tranactionId);

            if (!result)
            {
                _logger.LogWarning("Tranaction not found for deletion: {TranactionId}", tranactionId);
                return NotFound(new { message = _localizer["TransactionNotFound"].Value });
            }

            _logger.LogInformation("Tranaction deleted successfully: {TranactionId}", tranactionId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete tranaction: {TranactionId}", tranactionId);
            return ErrorResponse(_localizer["TransactionDeleteFailed"].Value);
        }
    }
}
