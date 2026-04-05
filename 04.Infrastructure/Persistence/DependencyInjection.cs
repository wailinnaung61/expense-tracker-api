using _04.Infrastructure.Services;
using Amazon;
using Amazon.EventBridge;
using Amazon.Runtime;
using Amazon.S3;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Application.Services;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Infrastructure.AWS.Configuration;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IInvestmentRepository, InvestmentRepository>();
        services.AddScoped<IInvestmentPortfolioRepository, InvestmentPortfolioRepository>();
        services.AddScoped<ISavingGoalRepository, SavingGoalRepository>();
        services.AddScoped<ISavingGoalContributionRepository, SavingGoalContributionRepository>();

        // In-memory cache for chat conversation history
        services.AddMemoryCache();

        // Register services
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<ITranactionService, TranactionService>();
        services.AddScoped<IAggregationService, AggregationService>();
        services.AddScoped<IRecurringPaymentService, RecurringPaymentService>();
        services.AddScoped<IBudgetService, BudgetService>();
        services.AddScoped<IInvestmentService, InvestmentService>();
        services.AddScoped<IInvestmentPortfolioService, InvestmentPortfolioService>();
        services.AddScoped<ISavingGoalService, SavingGoalService>();
        services.AddScoped<IChatService, ChatService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Notifications
        services.AddScoped<INotificationRepository, NotificationRepository>();
        // INotificationService registered in Program.cs (needs SharedResource localizer)

        // Export
        services.AddScoped<IExportJobRepository, ExportJobRepository>();
        services.AddScoped<IExportService, ExportService>();
        services.AddSingleton<IExportEventPublisher, EventBridgeExportPublisher>();
        services.AddSingleton<IExportFileService, S3ExportFileService>();

        // AWS EventBridge client
        services.AddSingleton<IAmazonEventBridge>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AwsSettings>>().Value;
            var creds = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
            return new AmazonEventBridgeClient(creds, RegionEndpoint.GetBySystemName(settings.Region));
        });

        // AWS S3 client (for pre-signed download URLs)
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<AwsSettings>>().Value;
            var creds = new BasicAWSCredentials(settings.AccessKey, settings.SecretKey);
            return new AmazonS3Client(creds, RegionEndpoint.GetBySystemName(settings.Region));
        });

        return services;
    }
}
