using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using expense_tracker_backend.Domain.Entities;
using expense_tracker_backend.Domain.Interfaces;
using expense_tracker_backend.Domain.Shared.Constants;
using expense_tracker_backend.Infrastructure.AWS.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace _04.Infrastructure.Services;

public class AggregationRepository : IAggregationRepository
{
    private readonly IAmazonDynamoDB _dynamoDb;
    private readonly string _tableName;
    private readonly ILogger<AggregationRepository> _logger;

    public AggregationRepository(
        IAmazonDynamoDB dynamoDb,
        IOptions<AwsSettings> awsSettings,
        ILogger<AggregationRepository> logger)
    {
        _dynamoDb = dynamoDb;
        _tableName = awsSettings.Value.DynamoDB.AggregationsTableName;
        _logger = logger;
    }

    public async Task UpdateAggregationsAsync(Transaction transaction)
    {
        var userId = transaction.UserId;
        var date = transaction.TransactionDate;
        var amount = transaction.Amount;
        var type = transaction.Type;
        var categoryId = transaction.CategoryId;

        var pk = $"USER#{userId}";
        var amountField = GetAmountFieldName(type);

        var tasks = new List<Task>
        {
            UpdateTimeAggregationAsync(pk, $"AGG#DAY#{date:yyyy-MM-dd}", date.ToString("yyyy/M/d"), amountField, amount),
            UpdateTimeAggregationAsync(pk, $"AGG#WEEK#{GetIsoWeek(date)}", GetIsoWeek(date), amountField, amount),
            UpdateMonthAggregationAsync(pk, $"AGG#MONTH#{date:yyyy-MM}", date, amountField, amount),
            UpdateTimeAggregationAsync(pk, $"AGG#YEAR#{date:yyyy}", date.ToString("yyyy"), amountField, amount),
            UpdateCategoryAggregationAsync(pk, type, categoryId, date, amount)
        };

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Aggregations updated for User: {UserId}, Type: {Type}, Amount: {Amount}, Date: {Date}",
            userId, type, amount, date);
    }

    private async Task UpdateTimeAggregationAsync(
        string pk, string sk, string period, string amountField, decimal amount)
    {
        var request = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = pk },
                ["SK"] = new() { S = sk }
            },
            UpdateExpression = $"SET #period = :period ADD {amountField} :amount, transactionCount :one",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#period"] = "period"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":period"] = new() { S = period },
                [":amount"] = new() { N = amount.ToString(CultureInfo.InvariantCulture) },
                [":one"] = new() { N = "1" }
            }
        };

        await _dynamoDb.UpdateItemAsync(request);
    }

    private async Task UpdateMonthAggregationAsync(
        string pk, string sk, DateTime date, string amountField, decimal amount)
    {
        var periodStart = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var request = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = pk },
                ["SK"] = new() { S = sk }
            },
            UpdateExpression = $"SET #period = :period, periodStart = :periodStart, periodEnd = :periodEnd ADD {amountField} :amount, transactionCount :one",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#period"] = "period"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":period"] = new() { S = date.ToString("yyyy/M") },
                [":periodStart"] = new() { S = periodStart.ToString("yyyy/MM/dd") },
                [":periodEnd"] = new() { S = periodEnd.ToString("yyyy/MM/dd") },
                [":amount"] = new() { N = amount.ToString(CultureInfo.InvariantCulture) },
                [":one"] = new() { N = "1" }
            }
        };

        await _dynamoDb.UpdateItemAsync(request);
    }

    private async Task UpdateCategoryAggregationAsync(
        string pk, AppConstants.TransactionType type, string categoryId, DateTime date, decimal amount)
    {
        var typeLabel = type.ToString().ToUpperInvariant();
        var monthKey = date.ToString("yyyy-MM");
        var sk = $"CAT#{typeLabel}#{categoryId}#{monthKey}";

        var periodStart = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1).AddDays(-1);

        var request = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = pk },
                ["SK"] = new() { S = sk }
            },
            UpdateExpression = "SET categoryId = :categoryId, #period = :period, periodStart = :periodStart, periodEnd = :periodEnd ADD totalAmount :amount, transactionCount :one",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#period"] = "period"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":categoryId"] = new() { S = categoryId },
                [":period"] = new() { S = date.ToString("yyyy/M") },
                [":periodStart"] = new() { S = periodStart.ToString("yyyy/MM/dd") },
                [":periodEnd"] = new() { S = periodEnd.ToString("yyyy/MM/dd") },
                [":amount"] = new() { N = amount.ToString(CultureInfo.InvariantCulture) },
                [":one"] = new() { N = "1" }
            }
        };

        await _dynamoDb.UpdateItemAsync(request);
    }

    private static string GetAmountFieldName(AppConstants.TransactionType type) => type switch
    {
        AppConstants.TransactionType.Income => "income",
        AppConstants.TransactionType.Expense => "expense",
        AppConstants.TransactionType.Savings => "saving",
        AppConstants.TransactionType.Investment => "investment",
        _ => "expense"
    };

    private static string GetIsoWeek(DateTime date)
    {
        var week = ISOWeek.GetWeekOfYear(date);
        var year = ISOWeek.GetYear(date);
        return $"{year}-W{week:D2}";
    }
}
