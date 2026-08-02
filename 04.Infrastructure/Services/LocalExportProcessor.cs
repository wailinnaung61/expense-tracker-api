using System.Globalization;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.ExportLocal;
using expense_tracker_backend.Application.ExportLocal.Models;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace _04.Infrastructure.Services;

/// <summary>
/// Ports export-lambda Function.ProcessMessageAsync into the API process
/// (skip EventBridge → SQS). Frontend still polls job status unchanged.
/// </summary>
public sealed class LocalExportProcessor : ILocalExportProcessor
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LocalExportProcessor> _logger;

    public LocalExportProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<LocalExportProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(ExportEventDetail detail)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessAsync(detail);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Local export background task crashed for job {JobId}", detail.JobId);
            }
        });
    }

    private async Task ProcessAsync(ExportEventDetail request)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var jobs = scope.ServiceProvider.GetRequiredService<IExportJobRepository>();
        var files = scope.ServiceProvider.GetRequiredService<IExportFileService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LocalExportProcessor>>();

        if (!Guid.TryParse(request.JobId, out var jobId))
        {
            logger.LogError("Invalid jobId {JobId}", request.JobId);
            return;
        }

        try
        {
            // Match lambda: stay PENDING until COMPLETED/FAILED (no PROCESSING step)
            var startDate = ParseDateBoundary(request.StartMonth, isStart: true);
            var endDate = ParseDateBoundary(request.EndMonth, isStart: false);

            var userEntity = await db.MemberProfiles.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == request.UserId)
                ?? throw new InvalidOperationException(
                    $"User {request.UserId} not found in member_profiles");

            var user = new UserProfile
            {
                UserId = userEntity.UserId,
                UserName = userEntity.UserName,
                Email = userEntity.Email,
                Currency = userEntity.Currency
            };

            var startStr = startDate.ToString("yyyy-MM-dd");
            var endStr = endDate.ToString("yyyy-MM-dd");

            var txEntities = await db.Transactions.AsNoTracking()
                .Include(t => t.Category)
                .Where(t => t.UserId == request.UserId
                            && t.TransactionDate.CompareTo(startStr) >= 0
                            && t.TransactionDate.CompareTo(endStr) <= 0)
                .OrderBy(t => t.TransactionDate)
                .ThenBy(t => t.CreatedAt)
                .ToListAsync();

            var transactions = txEntities.Select(t => new TransactionRow
            {
                TransactionId = t.TransactionId,
                Type = t.Type.ToString().ToUpperInvariant(),
                CategoryId = t.CategoryId ?? "",
                CategoryName = t.Category?.DisplayName ?? "Uncategorized",
                Amount = t.Amount,
                Description = t.Description,
                PaymentStatus = t.Status.ToString().ToUpperInvariant(),
                TransactionDate = DateOnly.ParseExact(t.TransactionDate, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture),
                Notes = t.Notes ?? ""
            }).ToList();

            var budgetRaw = await (
                from b in db.Budgets.AsNoTracking()
                join bc in db.BudgetCategories.AsNoTracking() on b.BudgetId equals bc.BudgetId
                join c in db.ExpenseCategories.AsNoTracking() on bc.CategoryId equals c.CategoryId into cj
                from c in cj.DefaultIfEmpty()
                where b.UserId == request.UserId
                      && b.StartDate.CompareTo(endStr) <= 0
                      && b.EndDate.CompareTo(startStr) >= 0
                orderby b.StartDate, bc.SortOrder
                select new
                {
                    bc.CategoryId,
                    CategoryName = c != null ? c.DisplayName : "Unknown",
                    bc.AllocatedAmount,
                    b.StartDate,
                    b.EndDate
                }).ToListAsync();

            var budgetRows = budgetRaw.Select(b => new BudgetCategoryRow
            {
                CategoryId = b.CategoryId,
                CategoryName = b.CategoryName,
                AllocatedAmount = b.AllocatedAmount,
                BudgetStart = DateOnly.ParseExact(b.StartDate, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture),
                BudgetEnd = DateOnly.ParseExact(b.EndDate, "yyyy-MM-dd",
                    CultureInfo.InvariantCulture)
            }).ToList();

            var excelBytes = ExcelReportBuilder.Build(
                user, transactions, budgetRows, startDate, endDate, request.Locale, logger);

            var fileName = $"report_{request.StartMonth}_{request.EndMonth}.xlsx";
            var s3Key = $"exports/{request.UserId}/{request.JobId}/{fileName}";

            await files.UploadObjectAsync(
                s3Key,
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

            await jobs.UpdateStatusAsync(jobId, ExportJobStatus.Completed, s3Key, fileName);
            logger.LogInformation(
                "Local export COMPLETED jobId={JobId} s3Key={S3Key}", jobId, s3Key);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Local export FAILED jobId={JobId}", jobId);
            try
            {
                var msg = ex.ToString();
                if (msg.Length > 1000) msg = msg[..1000];
                await jobs.UpdateStatusAsync(jobId, ExportJobStatus.Failed,
                    errorMessage: msg);
            }
            catch (Exception dbEx)
            {
                logger.LogError(dbEx, "Failed to mark job {JobId} as FAILED", jobId);
            }
        }
    }

    private static DateOnly ParseDateBoundary(string value, bool isStart)
    {
        if (DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var exact))
            return exact;

        var monthFirst = DateOnly.ParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture);
        return isStart ? monthFirst : monthFirst.AddMonths(1).AddDays(-1);
    }
}
