using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExpenseCategoryController : BaseController
    {
        private readonly IExpenseCategoryService _expenseCategoryService;
        private readonly ILogger<ExpenseCategoryController> _logger;
        private readonly IStringLocalizer<SharedResource> _localizer;

        public ExpenseCategoryController(
            IExpenseCategoryService expenseCategoryService,
            ILogger<ExpenseCategoryController> logger,
            IStringLocalizer<SharedResource> localizer)
        {
            _expenseCategoryService = expenseCategoryService;
            _logger = logger;
            _localizer = localizer;
        }

        /// <summary>
        /// Get expense categories with flexible filtering and pagination
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<ExpenseCategory>>> GetCategories([FromQuery] CategoryFilterRequest filter)
        {
            if (UserId is null)
                return Unauthorized();

            var result = await _expenseCategoryService.GetCategoriesAsync(UserId.Value, filter);
            return Ok(result);
        }

        /// <summary>
        /// Get Expense Category by ID
        /// </summary>
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ExpenseCategory>> GetById([FromRoute(Name = "id")] Guid categoryId)
        {
            if (UserId is null)
                return Unauthorized();

            _logger.LogInformation("Getting expense category by ID: {CategoryId}", categoryId);

            var category = await _expenseCategoryService.GetExpenseCategoryByIdAsync(UserId.Value, categoryId);

            if (category is null)
            {
                _logger.LogWarning("Expense category not found: {CategoryId}", categoryId);
                return NotFound(new { message = _localizer["CategoryNotFound"].Value });
            }

            _logger.LogInformation("Found expense category: {CategoryId}", categoryId);
            return Ok(category);
        }

        /// <summary>
        /// Create a new Expense Category
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ExpenseCategory>> Create([FromBody] CreateExpenseCategoryDto dto)
        {
            if (UserId is null)
                return Unauthorized();

            _logger.LogInformation("Creating new expense category: {CategoryName}", dto.DisplayName);

            try
            {
                var category = await _expenseCategoryService.CreateExpenseCategoryAsync(UserId.Value, dto);
                _logger.LogInformation("Expense category created successfully: {CategoryId}", category.CategoryId);
                return CreatedAtAction(nameof(GetById), new { id = category.CategoryId }, category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create expense category: {CategoryName}", dto.DisplayName);
                return ErrorResponse(_localizer["CategoryCreateFailed"].Value);
            }
        }

        /// <summary>
        /// Update an existing expense category
        /// </summary>
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ExpenseCategory>> Update([FromRoute(Name = "id")] Guid expenseCategoryId, [FromBody] UpdateExpenseCategoryDto dto)
        {
            if (UserId is null)
                return Unauthorized();

            _logger.LogInformation("Updating expense category: {CategoryId}", expenseCategoryId);
            try
            {
                var category = await _expenseCategoryService.UpdateExpenseCategoryAsync(UserId.Value, expenseCategoryId, dto);

                if (category is null)
                {
                    _logger.LogWarning("Expense category not found for update: {CategoryId}", expenseCategoryId);
                    return NotFound(new { message = _localizer["CategoryNotFound"].Value });
                }

                _logger.LogInformation("Expense category updated successfully: {CategoryId}", expenseCategoryId);
                return Ok(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update expense category: {CategoryId}", expenseCategoryId);
                return ErrorResponse(_localizer["CategoryUpdateFailed"].Value);
            }
        }

        /// <summary>
        /// Delete an expense category
        /// </summary>
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete([FromRoute(Name = "id")] Guid expenseCategoryId)
        {
            if (UserId is null)
                return Unauthorized();

            _logger.LogInformation("Deleting expense category: {CategoryId}", expenseCategoryId);

            try
            {
                var result = await _expenseCategoryService.DeleteExpenseCategoryAsync(UserId.Value, expenseCategoryId);
                if (!result)
                {
                    _logger.LogWarning("Expense category not found for deletion: {CategoryId}", expenseCategoryId);
                    return NotFound(new { message = _localizer["CategoryNotFound"].Value });
                }

                _logger.LogInformation("Expense category deleted successfully: {CategoryId}", expenseCategoryId);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete expense category: {CategoryId}", expenseCategoryId);
                return ErrorResponse(_localizer["CategoryDeleteFailed"].Value);
            }
        }
    }
}
