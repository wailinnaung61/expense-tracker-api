using _04.Infrastructure.Services;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Application.Services;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        // Redis connection multiplexer (singleton for lock support)
        var redisConnectionString = configuration.GetConnectionString("Redis")!;
        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnectionString));

        // Redis distributed cache
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = "ExpenseTracker:";
        });

        // Register repositories
        services.AddScoped<IMemberRepository, MemberRepository>();
        services.AddScoped<IExpenseCategoryRepository, ExpenseCategoryRepository>();
        services.AddScoped<ITranactionRepository, TranactionRepository>();
        services.AddScoped<IAggregationRepository, AggregationRepository>();
        services.AddScoped<IRecurringPaymentRepository, RecurringPaymentRepository>();
        services.AddScoped<ISavingGoalRepository, SavingGoalRepository>();

        // In-memory cache for chat conversation history
        services.AddMemoryCache();

        // Register services
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<ITranactionService, TranactionService>();
        services.AddScoped<IAggregationService, AggregationService>();
        services.AddScoped<IRecurringPaymentService, RecurringPaymentService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<ISavingGoalService, SavingGoalService>();

        return services;
    }
}
