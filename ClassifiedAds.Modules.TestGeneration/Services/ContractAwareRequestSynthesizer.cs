using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace ClassifiedAds.Modules.TestGeneration.Services;

internal sealed class ContractAwareRequestContext
{
    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public string OperationId { get; set; }

    public bool RequiresBody { get; set; }

    public bool RequiresAuth { get; set; }

    public bool IsRegisterLikeEndpoint { get; set; }

    public bool IsLoginLikeEndpoint { get; set; }

    public IReadOnlyCollection<string> RequiredPathParams { get; set; } = Array.Empty<string>();

    public IReadOnlyCollection<string> RequiredQueryParams { get; set; } = Array.Empty<string>();

    public IReadOnlyList<ParameterDetailDto> Parameters { get; set; } = Array.Empty<ParameterDetailDto>();

    public string RequestBodySchema { get; set; }

    public string RequestBodyExamples { get; set; }

    public string SuccessResponseSchema { get; set; }

    public Dictionary<string, string> PlaceholderByFieldName { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

internal sealed class ContractAwareRequestData
{
    public Dictionary<string, string> PathParams { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> QueryParams { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string BodyType { get; set; } = "None";

    public string Body { get; set; }

    public List<N8nTestCaseVariable> Variables { get; set; } = new();
}

internal static class ContractAwareRequestSynthesizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        WriteIndented = false,
    };

    private static readonly string[] TokenJsonPaths =
    {
        "$.data.token",
        "$.token",
        "$.data.accessToken",
        "$.accessToken",
        "$.data.jwt",
        "$.jwt",
    };

    public static ContractAwareRequestData BuildRequestData(
        ContractAwareRequestContext context,
        TestType testType)
    {
        var result = new ContractAwareRequestData
        {
            PathParams = BuildPathParams(context),
            QueryParams = BuildQueryParams(context),
            Headers = BuildHeaders(context),
        };

        var bodyNode = BuildBodyNode(context, testType);
        if (bodyNode != null)
        {
            ApplyPlaceholderHints(bodyNode, context);
            result.BodyType = InferBodyType(context, bodyNode);
            result.Body = bodyNode.ToJsonString(JsonOptions);
        }

        result.Variables = BuildVariables(context, testType, bodyNode);
        return result;
    }

    public static LlmSuggestedScenario RepairScenario(
        LlmSuggestedScenario scenario,
        ContractAwareRequestContext context)
    {
        var repair = BuildRequestData(context, scenario.SuggestedTestType);

        scenario.SuggestedPathParams = MergeDictionary(scenario.SuggestedPathParams, repair.PathParams);
        scenario.SuggestedQueryParams = MergeDictionary(scenario.SuggestedQueryParams, repair.QueryParams);
        scenario.SuggestedHeaders = MergeHeaders(scenario.SuggestedHeaders, repair.Headers);

        if (NeedsBodyRepair(scenario, context, repair))
        {
            scenario.SuggestedBodyType = repair.BodyType;
            scenario.SuggestedBody = repair.Body;
        }
        else if (string.IsNullOrWhiteSpace(scenario.SuggestedBodyType) &&
                 !string.IsNullOrWhiteSpace(scenario.SuggestedBody))
        {
            scenario.SuggestedBodyType = string.Equals(repair.BodyType, "None", StringComparison.OrdinalIgnoreCase)
                ? "JSON"
                : repair.BodyType;
        }

        scenario.Variables = MergeVariables(scenario.Variables, repair.Variables);
        return scenario;
    }

    private static Dictionary<string, string> BuildPathParams(ContractAwareRequestContext context)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameterName in context.RequiredPathParams ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                continue;
            }

            result[parameterName] = BuildScalarPlaceholderOrSample(context, parameterName);
        }

        return result;
    }

    private static Dictionary<string, string> BuildQueryParams(ContractAwareRequestContext context)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameterName in context.RequiredQueryParams ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(parameterName))
            {
                continue;
            }

            var parameter = context.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, parameterName, StringComparison.OrdinalIgnoreCase));
            result[parameterName] = BuildScalarPlaceholderOrSample(context, parameterName, parameter);
        }

        return result;
    }

    private static Dictionary<string, string> BuildHeaders(ContractAwareRequestContext context)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (context.RequiresAuth)
        {
            headers["Authorization"] = "Bearer {{authToken}}";
        }

        return headers;
    }

    private static JsonNode BuildBodyNode(ContractAwareRequestContext context, TestType testType)
    {
        if (!context.RequiresBody)
        {
            return null;
        }

        var baseNode = TryBuildFromExamples(context.RequestBodyExamples)
            ?? TryBuildFromSchema(context.RequestBodySchema)
            ?? TryBuildFromBodyParameters(context.Parameters);

        if (baseNode == null)
        {
            return new JsonObject();
        }

        if (testType == TestType.HappyPath)
        {
            return baseNode;
        }

        if (!TryParseSchema(context.RequestBodySchema, out var schemaRoot))
        {
            return baseNode;
        }

        var mutated = testType == TestType.Boundary
            ? TryBuildBoundaryMutation(baseNode, schemaRoot)
            : TryBuildNegativeMutation(baseNode, schemaRoot);

        return mutated ?? baseNode;
    }

    private static List<N8nTestCaseVariable> BuildVariables(
        ContractAwareRequestContext context,
        TestType testType,
        JsonNode bodyNode)
    {
        var result = new List<N8nTestCaseVariable>();
        if (testType != TestType.HappyPath)
        {
            return result;
        }

        if (bodyNode != null && context.IsRegisterLikeEndpoint)
        {
            if (TryFindJsonPath(bodyNode, "email", out var emailPath))
            {
                result.Add(new N8nTestCaseVariable
                {
                    VariableName = "registeredEmail",
                    ExtractFrom = "RequestBody",
                    JsonPath = emailPath,
                });
            }

            if (TryFindJsonPath(bodyNode, "password", out var passwordPath))
            {
                result.Add(new N8nTestCaseVariable
                {
                    VariableName = "registeredPassword",
                    ExtractFrom = "RequestBody",
                    JsonPath = passwordPath,
                });
            }
        }

        if (context.IsLoginLikeEndpoint || ResponseContainsToken(context.SuccessResponseSchema))
        {
            foreach (var jsonPath in TokenJsonPaths)
            {
                result.Add(new N8nTestCaseVariable
                {
                    VariableName = "authToken",
                    ExtractFrom = "ResponseBody",
                    JsonPath = jsonPath,
                });
            }

            result.Add(new N8nTestCaseVariable
            {
                VariableName = "authToken",
                ExtractFrom = "ResponseHeader",
                HeaderName = "Authorization",
                Regex = "(?:Bearer\\s+)?(?<value>[^\\s]+)$",
            });
        }

        return result
            .GroupBy(v => new
            {
                v.VariableName,
                v.ExtractFrom,
                v.JsonPath,
                v.HeaderName,
                v.Regex,
            })
            .Select(g => g.First())
            .ToList();
    }

    private static bool NeedsBodyRepair(
        LlmSuggestedScenario scenario,
        ContractAwareRequestContext context,
        ContractAwareRequestData repair)
    {
        if (!context.RequiresBody)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(scenario.SuggestedBody))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(scenario.SuggestedBodyType) ||
            string.Equals(scenario.SuggestedBodyType, "None", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedBodyType = NormalizeBodyType(repair?.BodyType);
        var currentBodyType = NormalizeBodyType(scenario.SuggestedBodyType);

        if (!string.IsNullOrWhiteSpace(expectedBodyType) &&
            !string.Equals(expectedBodyType, "NONE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(expectedBodyType, currentBodyType, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return string.Equals(expectedBodyType, "JSON", StringComparison.OrdinalIgnoreCase)
            ? IsMeaninglessStructuredBody(scenario.SuggestedBody, context.RequestBodySchema)
            : IsMeaninglessFieldBody(scenario.SuggestedBody, context.Parameters);
    }

    private static bool IsMeaninglessStructuredBody(string body, string requestBodySchema)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return true;
        }

        try
        {
            var node = JsonNode.Parse(body);
            if (node is not JsonObject obj || obj.Count > 0)
            {
                return false;
            }

            if (!TryParseSchema(requestBodySchema, out var schemaRoot))
            {
                return false;
            }

            return SchemaHasMeaningfulShape(schemaRoot);
        }
        catch
        {
            return false;
        }
    }

    private static bool SchemaHasMeaningfulShape(JsonElement schemaRoot)
    {
        if (schemaRoot.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (schemaRoot.TryGetProperty("required", out var required) &&
            required.ValueKind == JsonValueKind.Array &&
            required.GetArrayLength() > 0)
        {
            return true;
        }

        if (schemaRoot.TryGetProperty("properties", out var properties) &&
            properties.ValueKind == JsonValueKind.Object &&
            properties.EnumerateObject().Any())
        {
            return true;
        }

        return false;
    }

    private static bool IsMeaninglessFieldBody(string body, IReadOnlyList<ParameterDetailDto> parameters)
    {
        var bodyParameters = (parameters ?? Array.Empty<ParameterDetailDto>())
            .Where(parameter => parameter != null &&
                                string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(parameter.Name, "body", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (bodyParameters.Count == 0 || string.IsNullOrWhiteSpace(body))
        {
            return bodyParameters.Count > 0 && string.IsNullOrWhiteSpace(body);
        }

        try
        {
            var node = JsonNode.Parse(body);
            return node is JsonObject obj && obj.Count == 0;
        }
        catch
        {
            return false;
        }
    }

    private static Dictionary<string, string> MergeDictionary(
        Dictionary<string, string> current,
        Dictionary<string, string> fallback)
    {
        current ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        fallback ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var merged = new Dictionary<string, string>(fallback, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in current)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        return merged;
    }

    private static Dictionary<string, string> MergeHeaders(
        Dictionary<string, string> current,
        Dictionary<string, string> fallback)
    {
        current ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        fallback ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var merged = new Dictionary<string, string>(fallback, StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in current)
        {
            if (!string.IsNullOrWhiteSpace(kvp.Value))
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        return merged;
    }

    private static List<N8nTestCaseVariable> MergeVariables(
        List<N8nTestCaseVariable> current,
        List<N8nTestCaseVariable> fallback)
    {
        current ??= new List<N8nTestCaseVariable>();
        fallback ??= new List<N8nTestCaseVariable>();

        return current
            .Concat(fallback)
            .GroupBy(v => new
            {
                v.VariableName,
                v.ExtractFrom,
                v.JsonPath,
                v.HeaderName,
                v.Regex,
            })
            .Select(g => g.First())
            .ToList();
    }

    private static string BuildScalarPlaceholderOrSample(
        ContractAwareRequestContext context,
        string fieldName,
        ParameterDetailDto parameter = null)
    {
        var placeholder = GetPlaceholder(context, fieldName);
        if (!string.IsNullOrWhiteSpace(placeholder))
        {
            return placeholder;
        }

        if (parameter != null)
        {
            if (!string.IsNullOrWhiteSpace(parameter.DefaultValue))
            {
                return NormalizeScalarValue(parameter.DefaultValue);
            }

            var exampleNode = TryBuildFromExamples(parameter.Examples);
            if (exampleNode != null)
            {
                return ConvertNodeToString(exampleNode);
            }

            if (!string.IsNullOrWhiteSpace(parameter.Schema))
            {
                var schemaNode = TryBuildFromSchema(parameter.Schema, parameter.Name);
                if (schemaNode != null)
                {
                    return ConvertNodeToString(schemaNode);
                }
            }
        }

        return BuildHeuristicStringValue(fieldName, parameter?.DataType, parameter?.Format, null, null);
    }

    private static void ApplyPlaceholderHints(JsonNode node, ContractAwareRequestContext context)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                var placeholder = GetPlaceholder(context, property.Key);
                if (!string.IsNullOrWhiteSpace(placeholder) && IsScalarLike(property.Value))
                {
                    obj[property.Key] = placeholder;
                    continue;
                }

                if (property.Value != null)
                {
                    ApplyPlaceholderHints(property.Value, context);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item != null)
                {
                    ApplyPlaceholderHints(item, context);
                }
            }
        }
    }

    private static bool IsScalarLike(JsonNode node)
    {
        return node == null || node is JsonValue;
    }

    private static string GetPlaceholder(ContractAwareRequestContext context, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName) || context?.PlaceholderByFieldName == null)
        {
            return null;
        }

        if (!context.PlaceholderByFieldName.TryGetValue(fieldName, out var variableName) ||
            string.IsNullOrWhiteSpace(variableName))
        {
            return null;
        }

        return "{{" + variableName + "}}";
    }

    private static string InferBodyType(ContractAwareRequestContext context, JsonNode bodyNode)
    {
        var bodyParameters = (context?.Parameters ?? Array.Empty<ParameterDetailDto>())
            .Where(parameter => parameter != null &&
                                string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (bodyParameters.Any(parameter =>
                string.Equals(parameter.Name, "body", StringComparison.OrdinalIgnoreCase) &&
                (!string.IsNullOrWhiteSpace(parameter.Schema) ||
                 string.Equals(parameter.DataType, "object", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(parameter.DataType, "array", StringComparison.OrdinalIgnoreCase))))
        {
            return "JSON";
        }

        if (bodyParameters.Any(parameter =>
                !string.Equals(parameter.Name, "body", StringComparison.OrdinalIgnoreCase) &&
                IsFileLikeParameter(parameter)))
        {
            return "FormData";
        }

        if (bodyParameters.Any(parameter =>
                !string.Equals(parameter.Name, "body", StringComparison.OrdinalIgnoreCase)))
        {
            return "UrlEncoded";
        }

        return bodyNode is JsonValue ? "Raw" : "JSON";
    }

    private static bool IsFileLikeParameter(ParameterDetailDto parameter)
    {
        return string.Equals(parameter?.DataType, "file", StringComparison.OrdinalIgnoreCase)
            || string.Equals(parameter?.Format, "binary", StringComparison.OrdinalIgnoreCase)
            || parameter?.Name?.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string NormalizeBodyType(string bodyType)
    {
        if (string.IsNullOrWhiteSpace(bodyType))
        {
            return string.Empty;
        }

        return bodyType
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    private static JsonNode TryBuildFromBodyParameters(IReadOnlyList<ParameterDetailDto> parameters)
    {
        var bodyParameters = (parameters ?? Array.Empty<ParameterDetailDto>())
            .Where(p => p != null && string.Equals(p.Location, "Body", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (bodyParameters.Count == 0)
        {
            return null;
        }

        var body = new JsonObject();
        foreach (var parameter in bodyParameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
            {
                continue;
            }

            body[parameter.Name] = BuildParameterNode(parameter);
        }

        return body;
    }

    private static JsonNode BuildParameterNode(ParameterDetailDto parameter)
    {
        if (!string.IsNullOrWhiteSpace(parameter.Examples))
        {
            var fromExample = TryBuildFromExamples(parameter.Examples);
            if (fromExample != null)
            {
                return fromExample;
            }
        }

        if (!string.IsNullOrWhiteSpace(parameter.Schema))
        {
            var fromSchema = TryBuildFromSchema(parameter.Schema, parameter.Name);
            if (fromSchema != null)
            {
                return fromSchema;
            }
        }

        if (!string.IsNullOrWhiteSpace(parameter.DefaultValue))
        {
            var fromDefault = TryBuildFromExamples(parameter.DefaultValue);
            if (fromDefault != null)
            {
                return fromDefault;
            }
        }

        return JsonValue.Create(BuildHeuristicValue(parameter.Name, parameter.DataType, parameter.Format, null, null));
    }

    private static JsonNode TryBuildFromExamples(string examplesJson)
    {
        if (string.IsNullOrWhiteSpace(examplesJson))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(examplesJson);
            if (node == null)
            {
                return null;
            }

            if (node is JsonObject obj &&
                TryExtractNamedExampleValue(obj, out var exampleValue))
            {
                return exampleValue;
            }

            return node;
        }
        catch
        {
            return JsonValue.Create(NormalizeScalarValue(examplesJson));
        }
    }

    private static bool TryExtractNamedExampleValue(JsonObject examplesObject, out JsonNode exampleValue)
    {
        foreach (var property in examplesObject)
        {
            if (property.Value is JsonObject exampleObject &&
                exampleObject.TryGetPropertyValue("value", out var nestedValue) &&
                nestedValue != null)
            {
                exampleValue = nestedValue.DeepClone();
                return true;
            }

            if (property.Value != null)
            {
                exampleValue = property.Value.DeepClone();
                return true;
            }
        }

        exampleValue = null;
        return false;
    }

    private static JsonNode TryBuildFromSchema(string schemaJson, string propertyName = null)
    {
        if (!TryParseSchema(schemaJson, out var schemaRoot))
        {
            return null;
        }

        return BuildNodeFromSchema(schemaRoot, propertyName, depth: 0);
    }

    private static bool TryParseSchema(string schemaJson, out JsonElement schemaRoot)
    {
        if (!string.IsNullOrWhiteSpace(schemaJson))
        {
            try
            {
                using var document = JsonDocument.Parse(schemaJson);
                schemaRoot = document.RootElement.Clone();
                return true;
            }
            catch
            {
                // ignored
            }
        }

        schemaRoot = default;
        return false;
    }

    private static JsonNode BuildNodeFromSchema(JsonElement schema, string propertyName, int depth)
    {
        if (depth > 10)
        {
            return JsonValue.Create(BuildHeuristicValue(propertyName, null, null, null, null));
        }

        if (TryGetSchemaExample(schema, out var exampleNode))
        {
            return exampleNode;
        }

        if (TryGetSchemaDefault(schema, out var defaultNode))
        {
            return defaultNode;
        }

        if (TryGetEnumValue(schema, out var enumNode))
        {
            return enumNode;
        }

        if (TryBuildCompositeNode(schema, propertyName, depth, out var compositeNode))
        {
            return compositeNode;
        }

        var type = GetSchemaType(schema);
        return type switch
        {
            "object" => BuildObjectNode(schema, depth),
            "array" => BuildArrayNode(schema, propertyName, depth),
            "boolean" => JsonValue.Create(BuildBooleanValue()),
            "integer" => JsonValue.Create(BuildNumericValue(schema, integerOnly: true)),
            "number" => JsonValue.Create(BuildNumericValue(schema, integerOnly: false)),
            _ => JsonValue.Create(BuildStringValue(propertyName, schema)),
        };
    }

    private static bool TryBuildCompositeNode(
        JsonElement schema,
        string propertyName,
        int depth,
        out JsonNode compositeNode)
    {
        foreach (var keyword in new[] { "oneOf", "anyOf" })
        {
            if (!schema.TryGetProperty(keyword, out var variants) || variants.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var variant in variants.EnumerateArray())
            {
                var candidate = BuildNodeFromSchema(variant, propertyName, depth + 1);
                if (candidate != null)
                {
                    compositeNode = candidate;
                    return true;
                }
            }
        }

        if (schema.TryGetProperty("allOf", out var allOf) && allOf.ValueKind == JsonValueKind.Array)
        {
            JsonObject merged = null;
            JsonNode first = null;
            foreach (var variant in allOf.EnumerateArray())
            {
                var candidate = BuildNodeFromSchema(variant, propertyName, depth + 1);
                first ??= candidate;
                if (candidate is JsonObject candidateObject)
                {
                    merged ??= new JsonObject();
                    foreach (var property in candidateObject)
                    {
                        merged[property.Key] = property.Value?.DeepClone();
                    }
                }
            }

            compositeNode = merged ?? first;
            return compositeNode != null;
        }

        compositeNode = null;
        return false;
    }

    private static JsonNode BuildObjectNode(JsonElement schema, int depth)
    {
        var requiredNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    requiredNames.Add(item.GetString());
                }
            }
        }

        if (!schema.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Object)
        {
            return new JsonObject();
        }

        var result = new JsonObject();
        foreach (var property in properties.EnumerateObject())
        {
            if (requiredNames.Count > 0 &&
                !requiredNames.Contains(property.Name) &&
                !ShouldIncludeOptionalProperty(property.Name))
            {
                continue;
            }

            result[property.Name] = BuildNodeFromSchema(property.Value, property.Name, depth + 1);
        }

        return result;
    }

    private static JsonNode BuildArrayNode(JsonElement schema, string propertyName, int depth)
    {
        var result = new JsonArray();
        if (!schema.TryGetProperty("items", out var items))
        {
            return result;
        }

        result.Add(BuildNodeFromSchema(items, propertyName, depth + 1));
        return result;
    }

    private static bool ShouldIncludeOptionalProperty(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        return propertyName.EndsWith("Id", StringComparison.OrdinalIgnoreCase)
            || propertyName.IndexOf("name", StringComparison.OrdinalIgnoreCase) >= 0
            || propertyName.IndexOf("title", StringComparison.OrdinalIgnoreCase) >= 0
            || propertyName.IndexOf("description", StringComparison.OrdinalIgnoreCase) >= 0
            || propertyName.IndexOf("email", StringComparison.OrdinalIgnoreCase) >= 0
            || propertyName.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool TryGetSchemaExample(JsonElement schema, out JsonNode node)
    {
        if (schema.TryGetProperty("example", out var example))
        {
            node = JsonNode.Parse(example.GetRawText());
            return true;
        }

        if (schema.TryGetProperty("examples", out var examples) && examples.ValueKind != JsonValueKind.Null)
        {
            var fromExamples = TryBuildFromExamples(examples.GetRawText());
            if (fromExamples != null)
            {
                node = fromExamples;
                return true;
            }
        }

        node = null;
        return false;
    }

    private static bool TryGetSchemaDefault(JsonElement schema, out JsonNode node)
    {
        if (schema.TryGetProperty("default", out var defaultValue))
        {
            node = JsonNode.Parse(defaultValue.GetRawText());
            return true;
        }

        node = null;
        return false;
    }

    private static bool TryGetEnumValue(JsonElement schema, out JsonNode node)
    {
        if (schema.TryGetProperty("enum", out var enumValues) &&
            enumValues.ValueKind == JsonValueKind.Array &&
            enumValues.GetArrayLength() > 0)
        {
            node = JsonNode.Parse(enumValues[0].GetRawText());
            return true;
        }

        node = null;
        return false;
    }

    private static string GetSchemaType(JsonElement schema)
    {
        if (schema.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String)
        {
            return typeElement.GetString()?.Trim().ToLowerInvariant();
        }

        if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
        {
            return "object";
        }

        if (schema.TryGetProperty("items", out _))
        {
            return "array";
        }

        return "string";
    }

    private static decimal BuildNumericValue(JsonElement schema, bool integerOnly)
    {
        decimal? minimum = null;
        decimal? maximum = null;

        if (schema.TryGetProperty("minimum", out var minimumElement) &&
            minimumElement.TryGetDecimal(out var minimumValue))
        {
            minimum = minimumValue;
        }

        if (schema.TryGetProperty("maximum", out var maximumElement) &&
            maximumElement.TryGetDecimal(out var maximumValue))
        {
            maximum = maximumValue;
        }

        var value = minimum.HasValue
            ? Math.Max(minimum.Value, integerOnly ? 1 : 0.5m)
            : integerOnly ? 1m : 1.5m;

        if (maximum.HasValue)
        {
            value = Math.Min(value, maximum.Value);
        }

        return integerOnly ? decimal.Truncate(value) : value;
    }

    private static bool BuildBooleanValue() => true;

    private static string BuildStringValue(string propertyName, JsonElement schema)
    {
        var format = schema.TryGetProperty("format", out var formatElement) && formatElement.ValueKind == JsonValueKind.String
            ? formatElement.GetString()
            : null;

        int? minLength = null;
        if (schema.TryGetProperty("minLength", out var minLengthElement) && minLengthElement.TryGetInt32(out var parsedMinLength))
        {
            minLength = parsedMinLength;
        }

        int? maxLength = null;
        if (schema.TryGetProperty("maxLength", out var maxLengthElement) && maxLengthElement.TryGetInt32(out var parsedMaxLength))
        {
            maxLength = parsedMaxLength;
        }

        return BuildHeuristicStringValue(propertyName, "string", format, minLength, maxLength);
    }

    private static object BuildHeuristicValue(
        string propertyName,
        string dataType,
        string format,
        int? minLength,
        int? maxLength)
    {
        var normalizedType = dataType?.Trim().ToLowerInvariant();
        return normalizedType switch
        {
            "integer" or "int" or "long" => 1,
            "number" or "float" or "double" or "decimal" => 1.5m,
            "boolean" or "bool" => true,
            "file" or "binary" => BuildHeuristicStringValue(propertyName, dataType, format, minLength, maxLength),
            "array" => new[] { "sample-item" },
            "object" => new Dictionary<string, object>(),
            _ => BuildHeuristicStringValue(propertyName, dataType, format, minLength, maxLength),
        };
    }

    private static string BuildHeuristicStringValue(
        string propertyName,
        string dataType,
        string format,
        int? minLength,
        int? maxLength)
    {
        var name = propertyName ?? string.Empty;
        var normalizedFormat = format?.Trim().ToLowerInvariant();

        string value = normalizedFormat switch
        {
            "email" => "testuser@example.com",
            "uuid" => "00000000-0000-0000-0000-000000000001",
            "date" => "2024-01-01",
            "date-time" => "2024-01-01T00:00:00Z",
            "uri" => "https://example.com/resource",
            _ => null,
        };

        value ??= name.ToLowerInvariant() switch
        {
            var candidate when candidate.Contains("file") => "sample-file.txt",
            var candidate when candidate.Contains("image") => "sample-image.txt",
            var candidate when candidate.Contains("password") => "Test123!",
            var candidate when candidate.Contains("email") => "testuser@example.com",
            var candidate when candidate.Contains("username") => "testuser",
            var candidate when candidate.Contains("phone") => "+12025550123",
            var candidate when candidate.Contains("price") || candidate.Contains("amount") || candidate.Contains("cost") => "9.99",
            var candidate when candidate.Contains("quantity") || candidate.Contains("stock") || candidate.Contains("count") => "1",
            var candidate when candidate.EndsWith("id") => "1",
            var candidate when candidate.Contains("name") => "Sample Name",
            var candidate when candidate.Contains("title") => "Sample Title",
            var candidate when candidate.Contains("description") => "Sample description",
            _ => "sample-value",
        };

        if (minLength.HasValue && value.Length < minLength.Value)
        {
            value = value.PadRight(minLength.Value, 'x');
        }

        if (maxLength.HasValue && value.Length > maxLength.Value)
        {
            value = value[..maxLength.Value];
        }

        return value;
    }

    private static string NormalizeScalarValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        if ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) ||
            (trimmed.StartsWith('\'') && trimmed.EndsWith('\'')))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static string ConvertNodeToString(JsonNode node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var stringValue))
        {
            return stringValue;
        }

        return node.ToJsonString(JsonOptions).Trim('"');
    }

    private static JsonNode TryBuildBoundaryMutation(JsonNode baseNode, JsonElement schemaRoot)
    {
        var clone = baseNode.DeepClone();
        return TryApplyBoundaryMutation(clone, schemaRoot, propertyName: null)
            ? clone
            : null;
    }

    private static JsonNode TryBuildNegativeMutation(JsonNode baseNode, JsonElement schemaRoot)
    {
        var clone = baseNode.DeepClone();
        return TryApplyNegativeMutation(clone, schemaRoot, propertyName: null)
            ? clone
            : null;
    }

    private static bool TryApplyBoundaryMutation(JsonNode node, JsonElement schema, string propertyName)
    {
        if (node is JsonObject obj &&
            schema.TryGetProperty("properties", out var properties) &&
            properties.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in properties.EnumerateObject())
            {
                if (!obj.TryGetPropertyValue(property.Name, out var currentValue))
                {
                    continue;
                }

                if (TryMutateScalar(currentValue, property.Value, property.Name, boundary: true, out var mutated))
                {
                    obj[property.Name] = mutated;
                    return true;
                }

                if (currentValue != null && TryApplyBoundaryMutation(currentValue, property.Value, property.Name))
                {
                    return true;
                }
            }
        }

        if (node is JsonArray array &&
            array.Count > 0 &&
            schema.TryGetProperty("items", out var items) &&
            array[0] != null)
        {
            return TryApplyBoundaryMutation(array[0], items, propertyName);
        }

        return false;
    }

    private static bool TryApplyNegativeMutation(JsonNode node, JsonElement schema, string propertyName)
    {
        if (node is JsonObject obj)
        {
            if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in required.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var requiredName = item.GetString();
                    if (obj.ContainsKey(requiredName))
                    {
                        obj.Remove(requiredName);
                        return true;
                    }
                }
            }

            if (schema.TryGetProperty("properties", out var properties) && properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in properties.EnumerateObject())
                {
                    if (!obj.TryGetPropertyValue(property.Name, out var currentValue))
                    {
                        continue;
                    }

                    if (TryMutateScalar(currentValue, property.Value, property.Name, boundary: false, out var mutated))
                    {
                        obj[property.Name] = mutated;
                        return true;
                    }

                    if (currentValue != null && TryApplyNegativeMutation(currentValue, property.Value, property.Name))
                    {
                        return true;
                    }
                }
            }
        }

        if (node is JsonArray array && array.Count > 0)
        {
            array.Clear();
            return true;
        }

        return false;
    }

    private static bool TryMutateScalar(
        JsonNode currentValue,
        JsonElement schema,
        string propertyName,
        bool boundary,
        out JsonNode mutated)
    {
        var type = GetSchemaType(schema);
        switch (type)
        {
            case "string":
                mutated = JsonValue.Create(BuildBoundaryString(propertyName, schema, boundary));
                return true;
            case "integer":
            case "number":
                mutated = JsonValue.Create(BuildBoundaryNumber(schema, boundary, integerOnly: type == "integer"));
                return true;
            case "boolean":
                mutated = boundary
                    ? JsonValue.Create("not-a-boolean")
                    : JsonValue.Create("false-as-string");
                return true;
            default:
                mutated = null;
                return false;
        }
    }

    private static string BuildBoundaryString(string propertyName, JsonElement schema, bool boundary)
    {
        if (schema.TryGetProperty("minLength", out var minLengthElement) && minLengthElement.TryGetInt32(out var minLength))
        {
            var targetLength = Math.Max(minLength - 1, 0);
            return new string('x', targetLength);
        }

        if (schema.TryGetProperty("maxLength", out var maxLengthElement) && maxLengthElement.TryGetInt32(out var maxLength))
        {
            return new string('x', maxLength + 1);
        }

        if (schema.TryGetProperty("format", out var formatElement) &&
            formatElement.ValueKind == JsonValueKind.String &&
            string.Equals(formatElement.GetString(), "email", StringComparison.OrdinalIgnoreCase))
        {
            return "invalid-email";
        }

        return boundary ? string.Empty : BuildHeuristicStringValue(propertyName, "string", null, null, null) + "-invalid";
    }

    private static decimal BuildBoundaryNumber(JsonElement schema, bool boundary, bool integerOnly)
    {
        if (schema.TryGetProperty("minimum", out var minimumElement) && minimumElement.TryGetDecimal(out var minimum))
        {
            var adjusted = minimum - 1;
            return integerOnly ? decimal.Truncate(adjusted) : adjusted;
        }

        if (schema.TryGetProperty("maximum", out var maximumElement) && maximumElement.TryGetDecimal(out var maximum))
        {
            var adjusted = maximum + 1;
            return integerOnly ? decimal.Truncate(adjusted) : adjusted;
        }

        var fallback = boundary ? 0m : -1m;
        return integerOnly ? decimal.Truncate(fallback) : fallback;
    }

    private static bool TryFindJsonPath(JsonNode node, string propertyName, out string jsonPath)
    {
        return TryFindJsonPath(node, propertyName, "$", out jsonPath);
    }

    private static bool TryFindJsonPath(JsonNode node, string propertyName, string currentPath, out string jsonPath)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj)
            {
                var propertyPath = currentPath + "." + property.Key;
                if (string.Equals(property.Key, propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    jsonPath = propertyPath;
                    return true;
                }

                if (property.Value != null &&
                    TryFindJsonPath(property.Value, propertyName, propertyPath, out jsonPath))
                {
                    return true;
                }
            }
        }
        else if (node is JsonArray array)
        {
            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i];
                if (item != null &&
                    TryFindJsonPath(item, propertyName, $"{currentPath}[{i}]", out jsonPath))
                {
                    return true;
                }
            }
        }

        jsonPath = null;
        return false;
    }

    private static bool ResponseContainsToken(string schemaJson)
    {
        if (!TryParseSchema(schemaJson, out var schemaRoot))
        {
            return false;
        }

        return SchemaContainsProperty(schemaRoot, "token") || SchemaContainsProperty(schemaRoot, "accessToken");
    }

    private static bool SchemaContainsProperty(JsonElement schema, string propertyName)
    {
        if (schema.ValueKind == JsonValueKind.Object)
        {
            if (schema.TryGetProperty("properties", out var properties) &&
                properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in properties.EnumerateObject())
                {
                    if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (SchemaContainsProperty(property.Value, propertyName))
                    {
                        return true;
                    }
                }
            }

            foreach (var childKey in new[] { "allOf", "oneOf", "anyOf", "items" })
            {
                if (!schema.TryGetProperty(childKey, out var child))
                {
                    continue;
                }

                if (child.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in child.EnumerateArray())
                    {
                        if (SchemaContainsProperty(item, propertyName))
                        {
                            return true;
                        }
                    }
                }
                else if (SchemaContainsProperty(child, propertyName))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
