using System.Globalization;
using _04.Infrastructure.Services;
using expense_tracker_backend;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Application.Options;
using expense_tracker_backend.Application.Services;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using expense_tracker_backend.Filters;
using expense_tracker_backend.Middleware;
using expense_tracker_backend.Infrastructure.AWS.Cognito.Interfaces;
using expense_tracker_backend.Infrastructure.AWS.Cognito.Services;
using expense_tracker_backend.Infrastructure.AWS.Configuration;
using expense_tracker_backend.Infrastructure.Services;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Add services to the container
builder.Services.AddLocalization();

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Authorize button (JWT Bearer)
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Accept-Language header parameter on all endpoints
    options.OperationFilter<AcceptLanguageHeaderFilter>();
});

// AWS & Cognito
builder.Services.Configure<AwsSettings>(builder.Configuration.GetSection(AwsSettings.SectionName));
builder.Services.Configure<S3PresignOptions>(builder.Configuration.GetSection("AWS:S3"));

var awsSettings = builder.Configuration.GetSection(AwsSettings.SectionName).Get<AwsSettings>()!;
var credentials = AwsCredentialsProvider.Resolve(awsSettings);

builder.Services.AddSingleton<IAmazonCognitoIdentityProvider>(sp =>
    new AmazonCognitoIdentityProviderClient(credentials, RegionEndpoint.GetBySystemName(awsSettings.Region)));

// Authentication
var cognitoAuthority = $"https://cognito-idp.{awsSettings.Cognito.Region}.amazonaws.com/{awsSettings.Cognito.UserPoolId}";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = cognitoAuthority;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = cognitoAuthority,
        ValidateAudience = false,
        ValidateLifetime = true
    };
});

// CORS
var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Infrastructure services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ICognitoAuthService, CognitoAuthService>();

// Persistence (EF Core + Repositories)
builder.Services.AddPersistence(builder.Configuration);

// Notification localizer — register IStringLocalizer for NotificationService
builder.Services.AddScoped<IStringLocalizer>(sp =>
    sp.GetRequiredService<IStringLocalizer<SharedResource>>());
builder.Services.AddScoped<INotificationService>(sp =>
    new NotificationService(
        sp.GetRequiredService<INotificationRepository>(),
        sp.GetRequiredService<IMemberRepository>(),
        sp.GetRequiredService<IStringLocalizer<SharedResource>>()));

// Background services
builder.Services.AddHostedService<RecurringPaymentBackgroundService>();
builder.Services.AddHostedService<NotificationBackgroundService>();

var app = builder.Build();

// Auto-apply pending EF migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var migrateLog = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseMigrate");
    var pending = db.Database.GetPendingMigrations().ToList();
    if (pending.Count > 0)
        migrateLog.LogInformation("Applying {Count} pending EF migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
    else
        migrateLog.LogInformation("No pending EF migrations (schema already matches this build).");

    db.Database.Migrate();

    var stillPending = db.Database.GetPendingMigrations().ToList();
    if (stillPending.Count > 0)
        migrateLog.LogWarning("After Migrate(), these migrations are still pending (investigate): {Migrations}", string.Join(", ", stillPending));
    else
    {
        var latest = db.Database.GetAppliedMigrations().OrderByDescending(m => m).FirstOrDefault();
        migrateLog.LogInformation("EF migrations up to date. Latest applied migration id: {Latest}", latest ?? "(none)");
    }
}

// Configure the HTTP request pipeline.
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

var supportedCultures = new[]
{
    new CultureInfo("en"),
    new CultureInfo("my"),
    new CultureInfo("ja")
};

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseCors("AllowFrontend");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();
