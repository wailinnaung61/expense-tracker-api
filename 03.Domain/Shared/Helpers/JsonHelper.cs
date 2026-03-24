using System.Text.Json;
using System.Text.Json.Serialization;

namespace expense_tracker_backend.Domain.Shared.Helpers;

/// <summary>
/// JSON serialization helper methods
/// </summary>
public static class JsonHelper
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static readonly JsonSerializerOptions PrettyOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,

        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>
    /// Serialize object to JSON string
    /// </summary>
    public static string Serialize<T>(T obj, bool pretty = false) 
        => JsonSerializer.Serialize(obj, pretty ? PrettyOptions : DefaultOptions);

    /// <summary>
    /// Deserialize JSON string to object
    /// </summary>
    public static T? Deserialize<T>(string json) 
        => JsonSerializer.Deserialize<T>(json, DefaultOptions);

    /// <summary>
    /// Try deserialize JSON string to object
    /// </summary>
    public static bool TryDeserialize<T>(string json, out T? result)
    {
        try
        {
            result = JsonSerializer.Deserialize<T>(json, DefaultOptions);
            return result is not null;
        }
        catch
        {
            result = default;
            return false;
        }
    }

    /// <summary>
    /// Clone object via JSON serialization
    /// </summary>
    public static T? DeepClone<T>(T obj)
    {
        var json = Serialize(obj);
        return Deserialize<T>(json);
    }

    /// <summary>
    /// Check if string is valid JSON
    /// </summary>
    public static bool IsValidJson(string str)
    {
        if (string.IsNullOrWhiteSpace(str)) return false;
        
        try
        {
            JsonDocument.Parse(str);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
