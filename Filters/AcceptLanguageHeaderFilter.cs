using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace expense_tracker_backend.Filters;

public class AcceptLanguageHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Parameters ??= new List<OpenApiParameter>();

        operation.Parameters.Add(new OpenApiParameter
        {
            Name = "Accept-Language",
            In = ParameterLocation.Header,
            Required = false,
            Description = "Language preference (en, my, ja)",
            Schema = new OpenApiSchema
            {
                Type = "string",
                Enum = new List<IOpenApiAny>
                {
                    new OpenApiString("en"),
                    new OpenApiString("my"),
                    new OpenApiString("ja")
                },
                Default = new OpenApiString("en")
            }
        });
    }
}
