using System.Globalization;
using Amazon;
using Amazon.CognitoIdentityProvider;
using Amazon.Runtime;
using expense_tracker_backend.Infrastructure.AWS.Cognito.Interfaces;
using expense_tracker_backend.Infrastructure.AWS.Cognito.Services;
using expense_tracker_backend.Infrastructure.AWS.Configuration;
using expense_tracker_backend.Infrastructure.Services;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// AWS & Cognito
builder.Services.Configure<AwsSettings>(builder.Configuration.GetSection(AwsSettings.SectionName));

var awsSettings = builder.Configuration.GetSection(AwsSettings.SectionName).Get<AwsSettings>()!;
var credentials = new BasicAWSCredentials(awsSettings.AccessKey, awsSettings.SecretKey);

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

// Infrastructure services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ICognitoAuthService, CognitoAuthService>();

// Persistence (EF Core + Repositories)
builder.Services.AddPersistence(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
