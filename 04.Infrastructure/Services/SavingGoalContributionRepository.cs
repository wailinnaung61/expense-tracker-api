using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace _04.Infrastructure.Services;

public class SavingGoalContributionRepository : ISavingGoalContributionRepository
{
    private readonly ApplicationDbContext _context;

    public SavingGoalContributionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(List<SavingGoalContribution> Items, int TotalCount)> GetByGoalIdAsync(
        Guid userId, Guid savingGoalId, int pageSize, DateTime? cursor, Guid? cursorId)
    {
        var query = _context.SavingGoalContributions
            .AsNoTracking()
            .Where(c => c.UserId == userId.ToString()
                     && c.SavingGoalId == savingGoalId.ToString());

        var totalCount = await query.CountAsync();

        if (cursor.HasValue && cursorId.HasValue)
        {
            var cId = cursorId.Value.ToString();
            query = query.Where(c => c.CreatedAt < cursor.Value
                || (c.CreatedAt == cursor.Value && string.Compare(c.ContributionId, cId) < 0));
        }

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.ContributionId)
            .Take(pageSize + 1)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<SavingGoalContribution?> GetByIdAsync(Guid userId, Guid contributionId)
    {
        return await _context.SavingGoalContributions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId.ToString()
                                   && c.ContributionId == contributionId.ToString());
    }

    public async Task<SavingGoalContribution> CreateAsync(SavingGoalContribution contribution)
    {
        contribution.CreatedAt = DateTime.UtcNow;
        await _context.SavingGoalContributions.AddAsync(contribution);
        await _context.SaveChangesAsync();
        return contribution;
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid contributionId)
    {
        var contribution = await _context.SavingGoalContributions
            .FirstOrDefaultAsync(c => c.UserId == userId.ToString()
                                   && c.ContributionId == contributionId.ToString());
        if (contribution is null) return false;

        _context.SavingGoalContributions.Remove(contribution);
        await _context.SaveChangesAsync();
        return true;
    }
}
