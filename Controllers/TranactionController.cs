/*using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using expense_tracker_backend.Domain.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TranactionController : BaseController
{
    private readonly ITranactionService _expenseService;
    private readonly ILogger<TranactionController> _logger;

    public TranactionController(
        ITranactionService expenseService,
        ILogger<TranactionController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>
    /// Get transactions with filters and cursor-based pagination
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedResult<Tranaction>>> GetTransactions(
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] AppConstants.TransactionType? type,
        [FromQuery] AppConstants.PaymentStatus? status,
        [FromQuery] Guid? categoryId,
        [FromQuery] string? keyword,
        [FromQuery] PaginationRequest pagination)
    {
        if (UserId is null)
            return Unauthorized();

        if (startDate == default || endDate == default)
            return BadRequest(new { message = "startDate and endDate are required" });

        if (startDate > endDate)
            return BadRequest(new { message = "startDate cannot be greater than endDate" });

        _logger.LogInformation(
            "GetTransactions: UserId={UserId}, Date={StartDate}~{EndDate}, Type={Type}, Status={Status}, Category={CategoryId}, Keyword={Keyword}",
            UserId, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"), type, status, categoryId, keyword);

        try
        {
            var result = await _expenseService.GetByDateRangeWithFiltersAsync(
                UserId.Value, startDate, endDate, type, status, categoryId, keyword, pagination);

            _logger.LogInformation("Returning {Count} transactions", result.Items.Count());
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get transactions");
            return StatusCode(500, new { message = "Unable to retrieve transactions" });
        }
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
            return NotFound(new { message = $"Tranaction with ID {tranactionId} not found" });
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
            throw;
        }
    }

    /// <summary>
    /// Update an existing expense
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<Tranaction>> Update([FromRoute(Name = "id")] Guid tranactionId,[FromBody] UpdateTranactionDto dto)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Updating tranaction: {TranactionId},",tranactionId);
        try
        {
            var tranaction = await _expenseService.UpdateTranactionAsync(UserId.Value, tranactionId,dto);

            if (tranaction is null)
            {
                _logger.LogWarning("Tranaction not found for update: {TranactionId}", tranactionId);
                return NotFound(new { message = $"Tranaction with ID {tranactionId} not found" });  
            }

            _logger.LogInformation("Tranaction updated successfully: {TranactionId}", tranactionId);
            return Ok(tranaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tranaction: {TranactionId}", tranactionId);
            throw;
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
                return NotFound(new { message = $"Tranaction with ID {tranactionId} not found" });
            }

            _logger.LogInformation("Tranaction deleted successfully: {TranactionId}", tranactionId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete tranaction: {TranactionId}", tranactionId);
            throw;
        }
    }
}
*/