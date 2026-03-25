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
        await ApplyAggregationsAsync(transaction, 1);
    }

    public async Task ReverseAggregationsAsync(Transaction transaction)
    {
        await ApplyAggregationsAsync(transaction, -1);
    }

    private async Task ApplyAggregationsAsync(Transaction transaction, int direction)
    {
        var userId = transaction.UserId;
        var date = DateTime.Parse(transaction.TransactionDate);
        var amount = transaction.Amount * direction;
        var countDelta = direction;
        var type = transaction.Type;
        var categoryId = transaction.CategoryId;

        var pk = $"USER#{userId}";
        var amountField = GetAmountFieldName(type);

        var tasks = new List<Task>
        {
            UpdateTimeAggregationAsync(pk, $"AGG#DAY#{date:yyyy-MM-dd}", date.ToString("yyyy/M/d"), amountField, amount, countDelta),
            UpdateTimeAggregationAsync(pk, $"AGG#WEEK#{GetIsoWeek(date)}", GetIsoWeek(date), amountField, amount, countDelta),
            UpdateMonthAggregationAsync(pk, $"AGG#MONTH#{date:yyyy-MM}", date, amountField, amount, countDelta),
            UpdateTimeAggregationAsync(pk, $"AGG#YEAR#{date:yyyy}", date.ToString("yyyy"), amountField, amount, countDelta),
            UpdateCategoryAggregationAsync(pk, type, categoryId, date, amount, countDelta)
        };

        await Task.WhenAll(tasks);

        _logger.LogInformation(
            "Aggregations {Direction} for User: {UserId}, Type: {Type}, Amount: {Amount}, Date: {Date}",
            direction > 0 ? "incremented" : "decremented", userId, type, transaction.Amount, date);
    }

    private async Task UpdateTimeAggregationAsync(
        string pk, string sk, string period, string amountField, decimal amount, int countDelta)
    {
        var request = new UpdateItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = pk },
                ["SK"] = new() { S = sk }
            },
            UpdateExpression = $"SET #period = :period ADD {amountField} :amount, transactionCount :count",
            ExpressionAttributeNames = new Dictionary<string, string>
            {
                ["#period"] = "period"
            },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":period"] = new() { S = period },
                [":amount"] = new() { N = amount.ToString(CultureInfo.InvariantCulture) },
                [":count"] = new() { N = countDelta.ToString() }
            }
        };

        await _dynamoDb.UpdateItemAsync(request);
    }

    private async Task UpdateMonthAggregationAsync(
        string pk, string sk, DateTime date, string amountField, decimal amount, int countDelta)
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
            UpdateExpression = $"SET #period = :period, periodStart = :periodStart, periodEnd = :periodEnd ADD {amountField} :amount, transactionCount :count",
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
                [":count"] = new() { N = countDelta.ToString() }
            }
        };

        await _dynamoDb.UpdateItemAsync(request);
    }

    private async Task UpdateCategoryAggregationAsync(
        string pk, AppConstants.TransactionType type, string categoryId, DateTime date, decimal amount, int countDelta)
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
            UpdateExpression = "SET categoryId = :categoryId, #period = :period, periodStart = :periodStart, periodEnd = :periodEnd ADD totalAmount :amount, transactionCount :count",
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
                [":count"] = new() { N = countDelta.ToString() }
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

    // ============================================================================
    // DAILY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetDailyAggregationAsync(Guid userId, string date)
    {
        var pk = $"USER#{userId}";
        var sk = $"AGG#DAY#{date}";

        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = pk },
                ["SK"] = new() { S = sk }
            }
        });

        return response.Item.Count == 0 ? null : MapAggregationFromItem(response.Item);
    }

    public async Task<List<Aggregation>> GetDailyAggregationsRangeAsync(Guid userId, string startDate, string endDate)
    {
        var pk = $"USER#{userId}";

        var response = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :start AND :end",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = pk },
                [":start"] = new() { S = $"AGG#DAY#{startDate}" },
                [":end"] = new() { S = $"AGG#DAY#{endDate}" }
            }
        });

        return response.Items
            .Where(x => GetString(x, "SK").StartsWith("AGG#DAY#"))
            .Select(MapAggregationFromItem)
            .ToList();
    }

    // ============================================================================
    // WEEKLY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetWeeklyAggregationAsync(Guid userId, string week)
    {
        var pk = $"USER#{userId}";
        var sk = $"AGG#WEEK#{week}";

        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = pk },
                ["SK"] = new() { S = sk }
            }
        });

        return response.Item.Count == 0 ? null : MapAggregationFromItem(response.Item);
    }

    public async Task<List<Aggregation>> GetWeeklyAggregationsRangeAsync(Guid userId, string startWeek, string endWeek)
    {
        var pk = $"USER#{userId}";

        var response = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :start AND :end",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = pk },
                [":start"] = new() { S = $"AGG#WEEK#{startWeek}" },
                [":end"] = new() { S = $"AGG#WEEK#{endWeek}" }
            }
        });

        return response.Items
            .Where(x => GetString(x, "SK").StartsWith("AGG#WEEK#"))
            .Select(MapAggregationFromItem)
            .ToList();
    }

    // ============================================================================
    // MONTHLY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetMonthlyAggregationAsync(Guid userId, string month)
    {
        var pk = $"USER#{userId}";
        var sk = $"AGG#MONTH#{month}";

        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = pk },
                ["SK"] = new() { S = sk }
            }
        });

        return response.Item.Count == 0 ? null : MapAggregationFromItem(response.Item);
    }

    public async Task<List<Aggregation>> GetMonthlyAggregationsRangeAsync(Guid userId, string startMonth, string endMonth)
    {
        var pk = $"USER#{userId}";

        var response = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :start AND :end",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = pk },
                [":start"] = new() { S = $"AGG#MONTH#{startMonth}" },
                [":end"] = new() { S = $"AGG#MONTH#{endMonth}" }
            }
        });

        return response.Items
            .Where(x => GetString(x, "SK").StartsWith("AGG#MONTH#"))
            .Select(MapAggregationFromItem)
            .ToList();
    }

    // ============================================================================
    // YEARLY AGGREGATIONS
    // ============================================================================

    public async Task<Aggregation?> GetYearlyAggregationAsync(Guid userId, string year)
    {
        var pk = $"USER#{userId}";
        var sk = $"AGG#YEAR#{year}";

        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = pk },
                ["SK"] = new() { S = sk }
            }
        });

        return response.Item.Count == 0 ? null : MapAggregationFromItem(response.Item);
    }

    public async Task<List<Aggregation>> GetYearlyAggregationsRangeAsync(Guid userId, string startYear, string endYear)
    {
        var pk = $"USER#{userId}";

        var response = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND SK BETWEEN :start AND :end",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = pk },
                [":start"] = new() { S = $"AGG#YEAR#{startYear}" },
                [":end"] = new() { S = $"AGG#YEAR#{endYear}" }
            }
        });

        return response.Items
            .Where(x => GetString(x, "SK").StartsWith("AGG#YEAR#"))
            .Select(MapAggregationFromItem)
            .ToList();
    }

    // ============================================================================
    // CATEGORY AGGREGATIONS
    // ============================================================================

    public async Task<List<CategoryAggregation>> GetCategoryMonthlyAggregationsAsync(Guid userId, string month)
    {
        var pk = $"USER#{userId}";

        var response = await _dynamoDb.QueryAsync(new QueryRequest
        {
            TableName = _tableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :sk)",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new() { S = pk },
                [":sk"] = new() { S = "CAT#" }
            }
        });

        return response.Items
            .Where(x => GetString(x, "period") == month)
            .Select(MapCategoryFromItem)
            .ToList();
    }

    public async Task<CategoryAggregation?> GetCategoryMonthlyAggregationAsync(Guid userId, Guid categoryId, string month)
    {
        var pk = $"USER#{userId}";
        var sk = $"CAT#EXPENSE#{categoryId}#{month}";

        var response = await _dynamoDb.GetItemAsync(new GetItemRequest
        {
            TableName = _tableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = pk },
                ["SK"] = new() { S = sk }
            }
        });

        return response.Item.Count == 0 ? null : MapCategoryFromItem(response.Item);
    }

    // ============================================================================
    // MAPPERS & HELPERS
    // ============================================================================

    private static Aggregation MapAggregationFromItem(Dictionary<string, AttributeValue> item) => new()
    {
        Period = GetString(item, "period"),
        PeriodStart = GetString(item, "periodStart"),
        PeriodEnd = GetString(item, "periodEnd"),
        Income = GetDecimal(item, "income"),
        Expense = GetDecimal(item, "expense"),
        Saving = GetDecimal(item, "saving"),
        Investment = GetDecimal(item, "investment"),
        TransactionCount = GetInt(item, "transactionCount")
    };

    private static CategoryAggregation MapCategoryFromItem(Dictionary<string, AttributeValue> item) => new()
    {
        CategoryId = GetString(item, "categoryId"),
        Period = GetString(item, "period"),
        PeriodStart = GetString(item, "periodStart"),
        PeriodEnd = GetString(item, "periodEnd"),
        TotalAmount = GetDecimal(item, "totalAmount"),
        TransactionCount = GetInt(item, "transactionCount")
    };

    private static string GetString(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var v) ? v.S ?? "" : "";

    private static decimal GetDecimal(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var v) && decimal.TryParse(v.N, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;

    private static int GetInt(Dictionary<string, AttributeValue> item, string key) =>
        item.TryGetValue(key, out var v) && int.TryParse(v.N, out var i) ? i : 0;
}
