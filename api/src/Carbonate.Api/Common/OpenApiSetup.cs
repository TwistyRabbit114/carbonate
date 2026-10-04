using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Carbonate.Api.Common;

internal static class OpenApiSetup
{
    private const string Bearer = "Bearer";
    private const string ProblemJson = "application/problem+json";
    private const string ProblemSchema = "ProblemDetails";

    public static IServiceCollection AddCarbonateOpenApi(this IServiceCollection services)
    {
        // The schema generator reads the minimal-API JSON options, so enums are strings here too.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer(async (document, context, ct) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "Carbonate API",
                    Version = "v1",
                    Description = "Event coordination for Carbon Events and Carbon Logistics Management. "
                        + "Money fields are left out of a response when the caller may not see them.",
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes = new Dictionary<string, IOpenApiSecurityScheme>
                {
                    [Bearer] = new OpenApiSecurityScheme
                    {
                        Type = SecuritySchemeType.Http,
                        Scheme = "bearer",
                        BearerFormat = "JWT",
                        Description = "The access token from POST /api/auth/login or /api/auth/refresh.",
                    },
                };

                // One shared definition, referenced by every error response.
                document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
                document.Components.Schemas[ProblemSchema] =
                    await context.GetOrCreateSchemaAsync(typeof(ProblemDetails), cancellationToken: ct);
            });

            options.AddSchemaTransformer((schema, context, _) =>
            {
                // Integers are integers. The default also accepts numbers written as strings.
                if (schema.Type is { } type
                    && (type.HasFlag(JsonSchemaType.Integer) || type.HasFlag(JsonSchemaType.Number))
                    && type.HasFlag(JsonSchemaType.String))
                {
                    schema.Type = type & ~JsonSchemaType.String;
                    schema.Pattern = null;
                }

                if (context.JsonPropertyInfo is null
                    && context.JsonTypeInfo.Kind == JsonTypeInfoKind.Object
                    && schema.Properties is { Count: > 0 })
                {
                    MarkRequired(schema, context.JsonTypeInfo);
                }

                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var anonymous = context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any();
                if (!anonymous)
                {
                    operation.Security =
                    [
                        new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(Bearer, context.Document)] = [] },
                    ];
                }

                var method = context.Description.HttpMethod ?? "";
                var path = context.Description.RelativePath ?? "";
                var hasInput = context.Description.ParameterDescriptions.Count > 0;
                var hasPathParameter = path.Contains('{', StringComparison.Ordinal);
                var writes = method is "POST" or "PUT" or "PATCH";
                var changesState = method is "PUT" or "PATCH"
                    || (method == "POST" && StateVerbs.Any(v => path.EndsWith(v, StringComparison.Ordinal)));

                var problems = new List<int>();
                if (hasInput)
                {
                    problems.Add(StatusCodes.Status400BadRequest);
                }

                if (!anonymous)
                {
                    problems.Add(StatusCodes.Status401Unauthorized);
                    problems.Add(StatusCodes.Status403Forbidden);
                }

                if (hasPathParameter)
                {
                    problems.Add(StatusCodes.Status404NotFound);
                }

                if (changesState)
                {
                    problems.Add(StatusCodes.Status409Conflict);
                }

                if (writes)
                {
                    problems.Add(StatusCodes.Status422UnprocessableEntity);
                }

                var schema = new OpenApiSchemaReference(ProblemSchema, context.Document);
                operation.Responses ??= [];
                foreach (var status in problems.Distinct().Where(s => !operation.Responses.ContainsKey(s.ToString())))
                {
                    operation.Responses[status.ToString()] = new OpenApiResponse
                    {
                        Description = ProblemMeaning(status),
                        Content = new Dictionary<string, OpenApiMediaType> { [ProblemJson] = new() { Schema = schema } },
                    };
                }

                // The API speaks JSON only. ASP.NET also advertises text/plain, text/json and */*+json.
                foreach (var response in operation.Responses.Values.OfType<OpenApiResponse>())
                {
                    DropExtraMediaTypes(response.Content);
                }

                if (operation.RequestBody is OpenApiRequestBody body)
                {
                    DropExtraMediaTypes(body.Content);
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    public static WebApplication UseCarbonateOpenApi(this WebApplication app)
    {
        // The contract and the explorer are for development only (plan section 5, T1 section 7.3).
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

        app.MapOpenApi().AllowAnonymous();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Carbonate API v1"));
        return app;
    }

    /// <summary>
    /// A property that can never be null is required in the contract, so generated client types are not
    /// all optional. A field that is left out when masked carries JsonIgnore(WhenWritingNull) and stays optional.
    /// </summary>
    private static void MarkRequired(OpenApiSchema schema, JsonTypeInfo typeInfo)
    {
        var nullability = new NullabilityInfoContext();
        schema.Required ??= new HashSet<string>();

        foreach (var property in typeInfo.Properties)
        {
            if (property.AttributeProvider is not PropertyInfo info || !schema.Properties!.ContainsKey(property.Name))
            {
                continue;
            }

            var omittedWhenNull = info.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition
                == JsonIgnoreCondition.WhenWritingNull;
            var canBeNull = info.PropertyType.IsValueType
                ? Nullable.GetUnderlyingType(info.PropertyType) is not null
                : nullability.Create(info).ReadState != NullabilityState.NotNull;

            if (!canBeNull && !omittedWhenNull)
            {
                schema.Required.Add(property.Name);
            }
        }
    }

    private static void DropExtraMediaTypes(IDictionary<string, OpenApiMediaType>? content)
    {
        if (content is null)
        {
            return;
        }

        foreach (var type in content.Keys.Where(k => k is "text/plain" or "text/json" or "application/*+json").ToList())
        {
            content.Remove(type);
        }
    }

    private static readonly string[] StateVerbs =
    [
        "/transitions", "/submit", "/approve", "/issue", "/accept", "/reschedule", "/move",
        "/complete", "/return", "/mark-placed", "/mark-paid",
    ];

    private static string ProblemMeaning(int status) => status switch
    {
        400 => "A field is invalid. The body lists each one.",
        401 => "Not signed in, or the access token has expired.",
        403 => "Signed in, but missing the permission this needs.",
        404 => "Not found, or not visible to this user.",
        409 => "Someone else changed it first, or the state change is not allowed. See the problem type.",
        422 => "A business rule stops this.",
        _ => "Problem details.",
    };
}
