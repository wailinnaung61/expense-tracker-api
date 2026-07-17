using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RecurringPaymentController : BaseController
{
    private readonly IRecurringPaymentService _service;
    private readonly IExpenseCategoryService _categoryService;
    private readonly ILogger<RecurringPaymentController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public RecurringPaymentController(
        IRecurringPaymentService service,
        IExpenseCategoryService categoryService,
        ILogger<RecurringPaymentController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _service = service;
        _categoryService = categoryService;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Get all recurring payments
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<RecurringPaymentDto>>> GetAll()
    {
        if (UserId is null)
            return Unauthorized();

        var payments = await _service.GetAllAsync(UserId.Value);
        var dtos = await MapToDtosWithCategoryAsync(UserId.Value, payments);
        return Ok(dtos);
    }

    /// <summary>
    /// Get recurring payment by ID
    /// </summary>
    [HttpGet("{recurringId}")]
    public async Task<ActionResult<RecurringPaymentDto>> GetById(string recurringId)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting recurring payment by ID: {RecurringId}", recurringId);

        var payment = await _service.GetByIdAsync(UserId.Value, recurringId);

        if (payment is null)
        {
            _logger.LogWarning("Recurring payment not found: {RecurringId}", recurringId);
            return NotFound(new { message = _localizer["RecurringPaymentNotFound"].Value });
        }

        var category = await _categoryService.GetExpenseCategoryByIdAsync(UserId.Value, Guid.Parse(payment.CategoryId));

        _logger.LogInformation("Found recurring payment: {RecurringId}", recurringId);
        return Ok(MapToDto(payment, category?.DisplayName));
    }

    /// <summary>
    /// Get upcoming recurring payments by date range
    /// </summary>
    [HttpGet("upcoming")]
    public async Task<ActionResult<List<RecurringPaymentDto>>> GetUpcoming(
        [FromQuery] string startDate,
        [FromQuery] string endDate)
    {
        if (UserId is null)
            return Unauthorized();

        if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate))
            return BadRequest(new { message = _localizer["StartAndEndDateRequired"].Value });

        var payments = await _service.GetUpcomingAsync(UserId.Value, startDate, endDate);
        var dtos = await MapToDtosWithCategoryAsync(UserId.Value, payments);
        return Ok(dtos);
    }

    /// <summary>
    /// Create a new recurring payment
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<RecurringPaymentDto>> Create([FromBody] CreateRecurringPaymentRequest request)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Creating new recurring payment: {Name}, Amount: {Amount}, Category: {CategoryId}",
            request.Name, request.Amount, request.CategoryId);

        try
        {
            var payment = new RecurringPayment
            {
                RecurringId = Guid.NewGuid().ToString(),
                UserId = UserId.Value.ToString(),
                Name = request.Name,
                Amount = request.Amount,
                CategoryId = request.CategoryId.ToString(),
                Frequency = request.Frequency,
                NextDueDate = DateTime.SpecifyKind(DateTime.Parse(request.NextDueDate), DateTimeKind.Utc),
                AutoPay = request.AutoPay
            };

            var created = await _service.CreateAsync(UserId.Value, payment);
            var category = await _categoryService.GetExpenseCategoryByIdAsync(UserId.Value, Guid.Parse(created.CategoryId));

            _logger.LogInformation("Recurring payment created successfully: {RecurringId}", created.RecurringId);
            return CreatedAtAction(nameof(GetById), new { recurringId = created.RecurringId }, MapToDto(created, category?.DisplayName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create recurring payment: {Name}, Amount: {Amount}, Category: {CategoryId}",
                request.Name, request.Amount, request.CategoryId);
            return ErrorResponse(_localizer["RecurringPaymentCreateFailed"].Value);
        }
    }

    /// <summary>
    /// Mark a recurring payment as paid (Pay Now). Clears missed count and advances due date only if still overdue.
    /// </summary>
    [HttpPost("{recurringId}/mark-paid")]
    public async Task<ActionResult<RecurringPaymentDto>> MarkAsPaid(string recurringId)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Marking recurring payment as paid: {RecurringId}", recurringId);

        try
        {
            var payment = await _service.MarkAsPaidAsync(UserId.Value, recurringId);

            if (payment is null)
            {
                _logger.LogWarning("Recurring payment not found for mark-paid: {RecurringId}", recurringId);
                return NotFound(new { message = _localizer["RecurringPaymentNotFound"].Value });
            }

            var category = await _categoryService.GetExpenseCategoryByIdAsync(UserId.Value, Guid.Parse(payment.CategoryId));

            _logger.LogInformation("Recurring payment marked as paid: {RecurringId}", recurringId);
            return Ok(MapToDto(payment, category?.DisplayName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark recurring payment as paid: {RecurringId}", recurringId);
            return ErrorResponse(_localizer["RecurringPaymentMarkPaidFailed"].Value);
        }
    }

    /// <summary>
    /// Acknowledge a period as paid externally (e.g. manual expense). Clears MissedCount without creating a transaction.
    /// Use when the user forgot Pay Now but already recorded the payment elsewhere.
    /// </summary>
    [HttpPost("{recurringId}/acknowledge-paid")]
    public async Task<ActionResult<RecurringPaymentDto>> AcknowledgePaid(string recurringId)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Acknowledging recurring payment as paid externally: {RecurringId}", recurringId);

        try
        {
            var payment = await _service.AcknowledgePaidAsync(UserId.Value, recurringId);

            if (payment is null)
            {
                _logger.LogWarning("Recurring payment not found for acknowledge-paid: {RecurringId}", recurringId);
                return NotFound(new { message = _localizer["RecurringPaymentNotFound"].Value });
            }

            var category = await _categoryService.GetExpenseCategoryByIdAsync(UserId.Value, Guid.Parse(payment.CategoryId));

            _logger.LogInformation("Recurring payment acknowledged as paid: {RecurringId}, missed cleared", recurringId);
            return Ok(MapToDto(payment, category?.DisplayName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acknowledge recurring payment as paid: {RecurringId}", recurringId);
            return ErrorResponse(_localizer["RecurringPaymentAcknowledgeFailed"].Value);
        }
    }

    /// <summary>
    /// Update an existing recurring payment
    /// </summary>
    [HttpPut("{recurringId}")]
    public async Task<ActionResult<RecurringPaymentDto>> Update(string recurringId, [FromBody] UpdateRecurringPaymentRequest request)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Updating recurring payment: {RecurringId}", recurringId);

        try
        {
            var existing = await _service.GetByIdAsync(UserId.Value, recurringId);

            if (existing is null)
            {
                _logger.LogWarning("Recurring payment not found for update: {RecurringId}", recurringId);
                return NotFound(new { message = _localizer["RecurringPaymentNotFound"].Value });
            }

            existing.Name = request.Name;
            existing.Amount = request.Amount;
            existing.CategoryId = request.CategoryId.ToString();
            existing.Frequency = request.Frequency;
            existing.NextDueDate = DateTime.SpecifyKind(DateTime.Parse(request.NextDueDate), DateTimeKind.Utc);
            existing.Status = request.Status;
            existing.AutoPay = request.AutoPay;

            var updated = await _service.UpdateAsync(UserId.Value, existing);
            var category = await _categoryService.GetExpenseCategoryByIdAsync(UserId.Value, Guid.Parse(updated.CategoryId));

            _logger.LogInformation("Recurring payment updated successfully: {RecurringId}", recurringId);
            return Ok(MapToDto(updated, category?.DisplayName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update recurring payment: {RecurringId}", recurringId);
            return ErrorResponse(_localizer["RecurringPaymentUpdateFailed"].Value);
        }
    }

    /// <summary>
    /// Delete a recurring payment
    /// </summary>
    [HttpDelete("{recurringId}")]
    public async Task<IActionResult> Delete(string recurringId)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Deleting recurring payment: {RecurringId}", recurringId);

        try
        {
            var result = await _service.DeleteAsync(UserId.Value, recurringId);

            if (!result)
            {
                _logger.LogWarning("Recurring payment not found for deletion: {RecurringId}", recurringId);
                return NotFound(new { message = _localizer["RecurringPaymentNotFound"].Value });
            }

            _logger.LogInformation("Recurring payment deleted successfully: {RecurringId}", recurringId);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete recurring payment: {RecurringId}", recurringId);
            return ErrorResponse(_localizer["RecurringPaymentDeleteFailed"].Value);
        }
    }

    private async Task<List<RecurringPaymentDto>> MapToDtosWithCategoryAsync(Guid userId, List<RecurringPayment> payments)
    {
        var categoryIds = payments.Select(p => p.CategoryId).Distinct();
        var categories = new Dictionary<string, string>();

        foreach (var catId in categoryIds)
        {
            var cat = await _categoryService.GetExpenseCategoryByIdAsync(userId, Guid.Parse(catId));
            if (cat != null) categories[catId] = cat.DisplayName;
        }

        return payments.Select(p =>
        {
            categories.TryGetValue(p.CategoryId, out var categoryName);
            return MapToDto(p, categoryName);
        }).ToList();
    }

    private static RecurringPaymentDto MapToDto(RecurringPayment p, string? categoryName) => new(
        p.RecurringId,
        Guid.Parse(p.UserId),
        p.Name,
        p.Amount,
        Guid.Parse(p.CategoryId),
        categoryName,
        p.Frequency.ToString().ToUpper(),
        p.NextDueDate.ToString("yyyy-MM-dd"),
        p.LastPaidDate?.ToString("yyyy-MM-dd"),
        p.MissedCount,
        p.Status.ToString().ToUpper(),
        p.CreatedAt,
        p.UpdatedAt ?? p.CreatedAt,
        p.AutoPay
    );
}
