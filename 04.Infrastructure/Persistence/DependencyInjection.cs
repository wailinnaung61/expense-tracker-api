using _04.Infrastructure.Services;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Application.Services;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // Register repositories
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
        services.AddScoped<ITranactionRepository, TranactionRepository>();
        services.AddScoped<IAggregationRepository, AggregationRepository>();
        services.AddScoped<IRecurringPaymentRepository, RecurringPaymentRepository>();

        // Register services
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<ITranactionService, TranactionService>();
        services.AddScoped<IAggregationService, AggregationService>();
        services.AddScoped<IRecurringPaymentService, RecurringPaymentService>();

        return services;
    }
}
