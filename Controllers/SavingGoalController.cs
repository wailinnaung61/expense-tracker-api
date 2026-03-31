using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SavingGoalController : BaseController
{
    private readonly ISavingGoalService _service;
    private readonly ILogger<SavingGoalController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public SavingGoalController(
        ISavingGoalService service,
        ILogger<SavingGoalController> logger,
        IStringLocalizer<SharedResource> localizer)
    {
        _service = service;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Get all saving goals for the authenticated user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SavingGoalDto>>> GetAll()
    {
        if (UserId is null)
            return Unauthorized();

        var goals = await _service.GetSavingGoalsByUserIdAsync(UserId.Value.ToString());
        return Ok(goals);
    }

    /// <summary>
    /// Get saving goals filtered by status (Active, Paused, Completed)
    /// </summary>
    [HttpGet("status/{status}")]
    public async Task<ActionResult<List<SavingGoalDto>>> GetByStatus([FromRoute] string status)
    {
        if (UserId is null)
            return Unauthorized();

        if (!Enum.TryParse<AppConstants.RecurringStatus>(status, true, out var parsedStatus))
            return BadRequest(new { message = _localizer["InvalidStatus"].Value });

        var goals = await _service.GetSavingGoalsByStatusAsync(UserId.Value.ToString(), parsedStatus);
        return Ok(goals);
    }

    /// <summary>
    /// Get a saving goal by ID (includes contribution history)
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<SavingGoalDto>> GetById([FromRoute] string id)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Getting saving goal {SavingGoalId}", id);

        var goal = await _service.GetSavingGoalByIdAsync(UserId.Value.ToString(), id);
        if (goal is null)
        {
            _logger.LogWarning("Saving goal not found: {SavingGoalId}", id);
            return NotFound(new { message = _localizer["SavingGoalNotFound"].Value });
        }

        return Ok(goal);
    }

    /// <summary>
    /// Create a new saving goal
    /// </summary>
    [HttpPost("create")]
    public async Task<ActionResult<SavingGoalDto>> Create([FromBody] CreateSavingGoalDto dto)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Creating saving goal '{GoalName}' for user {UserId}", dto.GoalName, UserId.Value);

        try
        {
            var goal = await _service.CreateSavingGoalAsync(UserId.Value.ToString(), dto);
            _logger.LogInformation("Saving goal created: {SavingGoalId}", goal.SavingGoalId);
            return CreatedAtAction(nameof(GetById), new { id = goal.SavingGoalId }, goal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create saving goal '{GoalName}'", dto.GoalName);
            return ErrorResponse(_localizer["SavingGoalCreateFailed"].Value);
        }
    }

    /// <summary>
    /// Update an existing saving goal
    /// </summary>
    [HttpPut("{id}")]
    public async Task<ActionResult<SavingGoalDto>> Update([FromRoute] string id, [FromBody] UpdateSavingGoalDto dto)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Updating saving goal {SavingGoalId}", id);

        try
        {
            var goal = await _service.UpdateSavingGoalAsync(UserId.Value.ToString(), id, dto);
            if (goal is null)
            {
                _logger.LogWarning("Saving goal not found for update: {SavingGoalId}", id);
                return NotFound(new { message = _localizer["SavingGoalNotFound"].Value });
            }

            _logger.LogInformation("Saving goal updated: {SavingGoalId}", id);
            return Ok(goal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update saving goal {SavingGoalId}", id);
            return ErrorResponse(_localizer["SavingGoalUpdateFailed"].Value);
        }
    }

    /// <summary>
    /// Pause or resume a saving goal (toggle Active ↔ Paused)
    /// </summary>
    [HttpPatch("{id}/pause")]
    public async Task<ActionResult<SavingGoalDto>> PauseGoal([FromRoute] string id)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Pausing saving goal {SavingGoalId}", id);

        try
        {
            var current = await _service.GetSavingGoalByIdAsync(UserId.Value.ToString(), id);
            if (current is null)
                return NotFound(new { message = _localizer["SavingGoalNotFound"].Value });

            var newStatus = current.Status.Equals("PAUSED", StringComparison.OrdinalIgnoreCase)
                ? AppConstants.RecurringStatus.Active
                : AppConstants.RecurringStatus.Paused;

            var goal = await _service.PatchStatusAsync(UserId.Value.ToString(), id, newStatus);
            return Ok(goal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pause saving goal {SavingGoalId}", id);
            return ErrorResponse(_localizer["SavingGoalUpdateFailed"].Value);
        }
    }

    /// <summary>
    /// Delete a saving goal
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] string id)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Deleting saving goal {SavingGoalId}", id);

        var deleted = await _service.DeleteSavingGoalAsync(UserId.Value.ToString(), id);
        if (!deleted)
        {
            _logger.LogWarning("Saving goal not found for delete: {SavingGoalId}", id);
            return NotFound(new { message = _localizer["SavingGoalNotFound"].Value });
        }

        _logger.LogInformation("Saving goal deleted: {SavingGoalId}", id);
        return NoContent();
    }

    /// <summary>
    /// Add funds (contribution) to a saving goal
    /// </summary>
    [HttpPost("{id}/contributions")]
    public async Task<ActionResult<SavingGoalContributionDto>> AddContribution(
        [FromRoute] string id,
        [FromBody] AddContributionDto dto)
    {
        if (UserId is null)
            return Unauthorized();

        _logger.LogInformation("Adding contribution of {Amount} to saving goal {SavingGoalId}", dto.Amount, id);

        try
        {
            var goal = await _service.GetSavingGoalByIdAsync(UserId.Value.ToString(), id);
            if (goal is null)
                return NotFound(new { message = _localizer["SavingGoalNotFound"].Value });

            var contribution = await _service.AddContributionAsync(UserId.Value.ToString(), id, dto);
            _logger.LogInformation("Contribution added to saving goal {SavingGoalId}", id);
            return Ok(contribution);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add contribution to saving goal {SavingGoalId}", id);
            return ErrorResponse(_localizer["SavingGoalContributionFailed"].Value);
        }
    }

    /// <summary>
    /// Get contribution history for a saving goal
    /// </summary>
    [HttpGet("{id}/contributions")]
    public async Task<ActionResult<List<SavingGoalContributionDto>>> GetContributions([FromRoute] string id)
    {
        if (UserId is null)
            return Unauthorized();

        var contributions = await _service.GetContributionsAsync(UserId.Value.ToString(), id);
        return Ok(contributions);
    }
}
