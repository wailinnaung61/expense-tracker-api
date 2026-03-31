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

    public async Task<List<SavingGoalContribution>> GetByGoalIdAsync(string userId, string savingGoalId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(savingGoalId))
            return new List<SavingGoalContribution>();

        return await _context.SavingGoalContributions
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.SavingGoalId == savingGoalId)
            .OrderByDescending(c => c.ContributionDate)
            .ToListAsync();
    }

    public async Task<SavingGoalContribution?> GetByIdAsync(string userId, string contributionId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(contributionId))
            return null;

        return await _context.SavingGoalContributions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == userId && c.ContributionId == contributionId);
    }

    public async Task<SavingGoalContribution> CreateAsync(SavingGoalContribution contribution)
    {
        contribution.CreatedAt = DateTime.UtcNow;
        await _context.SavingGoalContributions.AddAsync(contribution);
        await _context.SaveChangesAsync();
        return contribution;
    }

    public async Task<decimal> GetTotalContributionsAsync(string savingGoalId)
    {
        if (string.IsNullOrEmpty(savingGoalId))
            return 0m;

        return await _context.SavingGoalContributions
            .Where(c => c.SavingGoalId == savingGoalId)
            .SumAsync(c => c.Amount);
    }
}
