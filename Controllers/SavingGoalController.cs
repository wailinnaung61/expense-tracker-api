using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace expense_tracker_backend.API.Controllers;

[ApiController]
[Route("api/savings")]
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

    /// <summary>GET /api/savings/dashboard</summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<SavingDashboardResponse>> GetDashboard()
    {
        if (UserId is null) return Unauthorized();
        var result = await _service.GetDashboardAsync(UserId.Value);
        return Ok(result);
    }

    /// <summary>GET /api/savings/goals?status=&keyword=&pageSize=&cursor=&cursorId=</summary>
    [HttpGet("goals")]
    public async Task<ActionResult<PagedResult<SavingGoalDto>>> GetAll([FromQuery] SavingGoalFilterRequest filter)
    {
        if (UserId is null) return Unauthorized();
        var result = await _service.GetGoalsAsync(UserId.Value, filter);
        return Ok(result);
    }

    /// <summary>GET /api/savings/goals/{id}</summary>
    [HttpGet("goals/{id:guid}")]
    public async Task<ActionResult<SavingGoalDto>> GetById([FromRoute] Guid id)
    {
        if (UserId is null) return Unauthorized();

        var goal = await _service.GetByIdAsync(UserId.Value, id);
        if (goal is null)
            return NotFound(new { message = _localizer["SavingGoalNotFound"].Value });

        return Ok(goal);
    }

    /// <summary>POST /api/savings/goals</summary>
    [HttpPost("goals")]
    public async Task<ActionResult<SavingGoalDto>> Create([FromBody] CreateSavingGoalRequest request)
    {
        if (UserId is null) return Unauthorized();

        try
        {
            var goal = await _service.CreateAsync(UserId.Value, request);
            _logger.LogInformation("Saving goal created: {SavingGoalId}", goal.SavingGoalId);
            return CreatedAtAction(nameof(GetById), new { id = goal.SavingGoalId }, goal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create saving goal");
            return ErrorResponse(_localizer["SavingGoalCreateFailed"].Value);
        }
    }

    /// <summary>PUT /api/savings/goals/{id}</summary>
    [HttpPut("goals/{id:guid}")]
    public async Task<ActionResult<SavingGoalDto>> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateSavingGoalRequest request)
    {
        if (UserId is null) return Unauthorized();

        try
        {
            var goal = await _service.UpdateAsync(UserId.Value, id, request);
            if (goal is null)
                return NotFound(new { message = _localizer["SavingGoalNotFound"].Value });

            _logger.LogInformation("Saving goal updated: {SavingGoalId}", id);
            return Ok(goal);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update saving goal {Id}", id);
            return ErrorResponse(_localizer["SavingGoalUpdateFailed"].Value);
        }
    }

    /// <summary>DELETE /api/savings/goals/{id}</summary>
    [HttpDelete("goals/{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        if (UserId is null) return Unauthorized();

        var deleted = await _service.DeleteAsync(UserId.Value, id);
        if (!deleted)
            return NotFound(new { message = _localizer["SavingGoalNotFound"].Value });

        _logger.LogInformation("Saving goal deleted: {SavingGoalId}", id);
        return NoContent();
    }

    /// <summary>GET /api/savings/goals/{id}/contributions</summary>
    [HttpGet("goals/{id:guid}/contributions")]
    public async Task<ActionResult<PagedResult<SavingGoalContributionDto>>> GetContributions(
        [FromRoute] Guid id,
        [FromQuery] int pageSize = 10,
        [FromQuery] DateTime? cursor = null,
        [FromQuery] Guid? cursorId = null)
    {
        if (UserId is null) return Unauthorized();

        var result = await _service.GetContributionsAsync(UserId.Value, id, pageSize, cursor, cursorId);
        return Ok(result);
    }

    /// <summary>POST /api/savings/goals/{id}/contributions — deposit or withdraw</summary>
    [HttpPost("goals/{id:guid}/contributions")]
    public async Task<ActionResult<SavingGoalContributionDto>> AddContribution(
        [FromRoute] Guid id,
        [FromBody] AddSavingContributionRequest request)
    {
        if (UserId is null) return Unauthorized();

        try
        {
            var contribution = await _service.AddContributionAsync(UserId.Value, id, request);
            _logger.LogInformation("Contribution added to saving goal {SavingGoalId}", id);
            return Ok(contribution);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add contribution to saving goal {Id}", id);
            return ErrorResponse(_localizer["SavingGoalContributionFailed"].Value);
        }
    }

    /// <summary>DELETE /api/savings/goals/{id}/contributions/{contributionId}</summary>
    [HttpDelete("goals/{id:guid}/contributions/{contributionId:guid}")]
    public async Task<IActionResult> DeleteContribution(
        [FromRoute] Guid id,
        [FromRoute] Guid contributionId)
    {
        if (UserId is null) return Unauthorized();

        var deleted = await _service.DeleteContributionAsync(UserId.Value, id, contributionId);
        if (!deleted)
            return NotFound(new { message = _localizer["SavingGoalContributionNotFound"].Value });

        return NoContent();
    }
}

