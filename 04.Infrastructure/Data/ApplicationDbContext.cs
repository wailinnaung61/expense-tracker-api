using expense_tracker_backend.Domain.Entities;
using Infrastructure.Data.Seed;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<MemberProfile> MemberProfiles { get; set; }
    public DbSet<ExpenseCategory> ExpenseCategories { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<RecurringPayment> RecurringPayments { get; set; }
    public DbSet<Budget> Budgets { get; set; }
    public DbSet<BudgetCategory> BudgetCategories { get; set; }
    public DbSet<BudgetSnapshot> BudgetSnapshots { get; set; }
    public DbSet<Investment> Investments { get; set; }
    public DbSet<InvestmentPortfolio> InvestmentPortfolios { get; set; }
    public DbSet<SavingGoal> SavingGoals { get; set; }
    public DbSet<SavingGoalContribution> SavingGoalContributions { get; set; }
    public DbSet<ExportJob> ExportJobs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        DatabaseSeeder.SeedData(modelBuilder);
    }
}
