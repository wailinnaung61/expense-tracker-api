using System.Text.Json.Serialization;

namespace expense_tracker_backend.Application.DTOs;

public record ChatRequest(string Message);

public record ChatResponse(
    string Message,
    [property: JsonPropertyName("name")]
    string? Name = null,
    [property: JsonPropertyName("refreshTarget")]
    string? RefreshTarget = null,
    [property: JsonPropertyName("functionsCalled")]
    List<FunctionCallResult>? FunctionsCalled = null,
    [property: JsonPropertyName("createdAt")]
    DateTime CreatedAt = default
);

public record FunctionCallResult(
    string FunctionName,
    [property: JsonPropertyName("result")]
    object? Result = null
);
