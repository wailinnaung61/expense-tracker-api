using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace _04.Infrastructure.Services;

public class SavingGoalRepository : ISavingGoalRepository
{
    private readonly ApplicationDbContext _context;

    public SavingGoalRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<SavingGoal?> GetByIdAsync(string userId, string savingGoalId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(savingGoalId))
            return null;

        return await _context.SavingGoals
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SavingGoalId == savingGoalId);
    }

    public async Task<List<SavingGoal>> GetByUserIdAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return new List<SavingGoal>();

        return await _context.SavingGoals
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SavingGoal>> GetByCategoryIdAsync(string userId, string categoryId)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(categoryId))
            return new List<SavingGoal>();

        return await _context.SavingGoals
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.CategoryId == categoryId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SavingGoal>> GetByStatusAsync(string userId, AppConstants.RecurringStatus status)
    {
        if (string.IsNullOrEmpty(userId))
            return new List<SavingGoal>();

        return await _context.SavingGoals
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.Status == status)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();
    }

    public async Task<SavingGoal> CreateAsync(SavingGoal savingGoal)
    {
        savingGoal.CreatedAt = DateTime.UtcNow;
        savingGoal.UpdatedAt = DateTime.UtcNow;

        await _context.SavingGoals.AddAsync(savingGoal);
        await _context.SaveChangesAsync();

        return savingGoal;
    }

    public async Task<SavingGoal?> UpdateAsync(SavingGoal savingGoal)
    {
        var existing = await _context.SavingGoals
            .FirstOrDefaultAsync(s => s.UserId == savingGoal.UserId && s.SavingGoalId == savingGoal.SavingGoalId);

        if (existing == null)
            return null;

        existing.GoalName = savingGoal.GoalName;
        existing.TargetAmount = savingGoal.TargetAmount;
        existing.InitialDeposit = savingGoal.InitialDeposit;
        existing.TargetDate = savingGoal.TargetDate;
        existing.RecurringType = savingGoal.RecurringType;
        existing.Icon = savingGoal.Icon;
        existing.Color = savingGoal.Color;
        existing.Status = savingGoal.Status;
        existing.UpdatedAt = DateTime.UtcNow;

        _context.SavingGoals.Update(existing);
        await _context.SaveChangesAsync();

        return existing;
    }

    public async Task<bool> DeleteAsync(string userId, string savingGoalId)
    {
        var savingGoal = await _context.SavingGoals
            .FirstOrDefaultAsync(s => s.UserId == userId && s.SavingGoalId == savingGoalId);

        if (savingGoal == null)
            return false;

        _context.SavingGoals.Remove(savingGoal);
        await _context.SaveChangesAsync();

        return true;
    }
}
