using System.Globalization;
using expense_tracker_backend.Application.DTOs;
using expense_tracker_backend.Application.Interfaces;
using expense_tracker_backend.Application.Options;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using Microsoft.Extensions.Options;

namespace expense_tracker_backend.Application.Services;

public class BudgetReportService : IBudgetReportService
{
    public const string ExportTypeBudgetExcel = "budget-excel";

    private readonly IBudgetRepository _budgetRepository;
    private readonly ITranactionRepository _transactions;
    private readonly IMemberRepository _memberRepository;
    private readonly IBudgetReportWorkbookBuilder _workbookBuilder;
    private readonly IExportFileService _fileService;
    private readonly IExportJobRepository _exportJobRepository;
    private readonly IOptions<S3PresignOptions> _presignOptions;

    public BudgetReportService(
        IBudgetRepository budgetRepository,
        ITranactionRepository transactions,
        IMemberRepository memberRepository,
        IBudgetReportWorkbookBuilder workbookBuilder,
        IExportFileService fileService,
        IExportJobRepository exportJobRepository,
        IOptions<S3PresignOptions> presignOptions)
    {
        _budgetRepository = budgetRepository;
        _transactions = transactions;
        _memberRepository = memberRepository;
        _workbookBuilder = workbookBuilder;
        _fileService = fileService;
        _exportJobRepository = exportJobRepository;
        _presignOptions = presignOptions;
    }

    public async Task<BudgetReportExcelResponse?> CreateExcelReportAsync(
        Guid userId, string budgetId, CancellationToken cancellationToken = default)
    {
        var budget = await _budgetRepository.GetByIdAsync(userId.ToString(), budgetId);
        if (budget is null) return null;

        var profile = await _memberRepository.GetProfileByUserIdAsync(userId.ToString());
        var currency = string.IsNullOrWhiteSpace(profile?.Currency) ? "JPY" : profile!.Currency;

        var categoryIds = budget.BudgetCategories.Select(bc => bc.CategoryId).Distinct().ToList();
        var spentByCategory = await _transactions.GetCompletedExpenseTotalsByCategoryAsync(
            userId.ToString(), budget.StartDate, budget.EndDate, categoryIds);

        var bytes = _workbookBuilder.Build(budget, currency, profile?.UserName, spentByCategory);
        var safeFile = BuildBudgetExcelFileName(budget);
        var key = $"exports/{userId}/{safeFile}";

        await _fileService.UploadObjectAsync(
            key,
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            cancellationToken);

        var job = new ExportJob
        {
            UserId = userId.ToString(),
            Status = ExportJobStatus.Completed,
            Type = ExportTypeBudgetExcel,
            StartMonth = budget.StartDate,
            EndMonth = budget.EndDate,
            BudgetId = budgetId,
            S3Key = key,
            FileName = safeFile,
            CompletedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _exportJobRepository.CreateAsync(job);

        return new BudgetReportExcelResponse(job.Id, job.Status, safeFile, job.CreatedAt);
    }

    public async Task<ExportDownloadResponse?> GetReportDownloadUrlAsync(Guid userId, Guid jobId)
    {
        var job = await _exportJobRepository.GetByIdAsync(jobId, userId);
        if (job is null || job.Status != ExportJobStatus.Completed || job.S3Key is null)
            return null;

        if (!string.Equals(job.Type, ExportTypeBudgetExcel, StringComparison.OrdinalIgnoreCase))
            return null;

        var expiry = Math.Clamp(_presignOptions.Value.PresignedUrlExpiryMinutes, 1, 10080);
        var url = await _fileService.GenerateDownloadUrlAsync(job.S3Key, expiry);
        if (url is null) return null;

        var fileName = job.FileName ?? Path.GetFileName(job.S3Key);
        return new ExportDownloadResponse(url, fileName ?? "budget.xlsx", DateTime.UtcNow.AddMinutes(expiry));
    }

    /// <summary>
    /// Safe S3 object name: <c>budget_yyyy-MM-dd_yyyy-MM-dd.xlsx</c> (hyphens, not slashes, so the key stays a single leaf name).
    /// </summary>
    internal static string BuildBudgetExcelFileName(Budget budget)
    {
        if (DateOnly.TryParse(budget.StartDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var start)
            && DateOnly.TryParse(budget.EndDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
        {
            return $"budget_{start:yyyy-MM-dd}_{end:yyyy-MM-dd}.xlsx";
        }

        static string Sanitize(string s) =>
            string.Join("-", s.Split('/', '\\', '.', ' ').Where(static p => p.Length > 0));

        return $"budget_{Sanitize(budget.StartDate)}_{Sanitize(budget.EndDate)}.xlsx";
    }
}
