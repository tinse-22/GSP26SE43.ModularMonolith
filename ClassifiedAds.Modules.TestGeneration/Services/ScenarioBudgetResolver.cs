using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public sealed class ScenarioBudgetResolver
{
    private readonly ScenarioGenerationBudgetOptions _options;

    public ScenarioBudgetResolver(ScenarioGenerationBudgetOptions options)
    {
        _options = Normalize(options);
    }

    public ScenarioBudget Resolve(
        ApiOrderItemModel endpoint,
        ApiEndpointMetadataDto metadata,
        string businessContext = null,
        int coverableRequirementCount = 0,
        int dependencyRequirementCount = 0)
    {
        var method = ResolveHttpMethod(endpoint, metadata);
        var hasRequestBody = HasRequestBody(metadata);
        var hasRequestSchema = HasRequestSchema(metadata);
        var requiredFieldCount = CountRequiredFields(metadata);
        var pathQueryParameterCount = CountPathQueryParameters(endpoint, metadata);
        var nonSuccessResponseCount = metadata?.Responses?.Count(x => x.StatusCode >= 400 && x.StatusCode <= 599) ?? 0;
        var hasBusinessContext = !string.IsNullOrWhiteSpace(businessContext);
        var hasMappedRequirements = coverableRequirementCount > 0;
        var isSimpleMethod = IsSimpleMethod(method);
        var isComplexEndpoint = !isSimpleMethod || hasRequestBody || hasRequestSchema;
        var softLimit = isComplexEndpoint
            ? _options.ComplexEndpointSoftLimit
            : _options.SimpleEndpointSoftLimit;
        var hardLimit = Math.Max(softLimit, _options.DefaultHardLimitPerEndpoint);

        var complexityBonus = 0;
        if (requiredFieldCount >= 4)
        {
            complexityBonus += 2;
        }
        else if (requiredFieldCount >= 2)
        {
            complexityBonus += 1;
        }

        if (pathQueryParameterCount >= 3)
        {
            complexityBonus += 1;
        }

        if (nonSuccessResponseCount >= 3)
        {
            complexityBonus += 1;
        }

        if (hasMappedRequirements)
        {
            complexityBonus += 1;
        }

        var target = isComplexEndpoint
            ? softLimit + complexityBonus
            : softLimit + Math.Min(complexityBonus, 2);
        target = Math.Clamp(target, 1, hardLimit);

        var reasons = new List<string>
        {
            $"{method} endpoint",
        };

        if (hasRequestBody)
        {
            reasons.Add("request body");
        }
        else if (hasRequestSchema)
        {
            reasons.Add("request schema");
        }

        if (requiredFieldCount > 0)
        {
            reasons.Add($"{requiredFieldCount} required field/parameter(s)");
        }

        if (pathQueryParameterCount > 0)
        {
            reasons.Add($"{pathQueryParameterCount} path/query parameter(s)");
        }

        if (nonSuccessResponseCount > 0)
        {
            reasons.Add($"{nonSuccessResponseCount} documented error response(s)");
        }

        if (hasBusinessContext)
        {
            reasons.Add("business context");
        }

        if (hasMappedRequirements)
        {
            reasons.Add($"{coverableRequirementCount} mapped requirement(s)");
        }

        if (dependencyRequirementCount > 0)
        {
            reasons.Add($"{dependencyRequirementCount} dependency requirement(s)");
        }

        return new ScenarioBudget
        {
            SoftLimit = softLimit,
            HardLimit = hardLimit,
            Target = target,
            Reason = string.Join(", ", reasons),
        };
    }

    public static ScenarioGenerationBudgetOptions Normalize(ScenarioGenerationBudgetOptions options)
    {
        options ??= new ScenarioGenerationBudgetOptions();
        var defaults = new ScenarioGenerationBudgetOptions();

        return new ScenarioGenerationBudgetOptions
        {
            SimpleEndpointSoftLimit = options.SimpleEndpointSoftLimit <= 0 ? defaults.SimpleEndpointSoftLimit : options.SimpleEndpointSoftLimit,
            ComplexEndpointSoftLimit = options.ComplexEndpointSoftLimit <= 0 ? defaults.ComplexEndpointSoftLimit : options.ComplexEndpointSoftLimit,
            DefaultHardLimitPerEndpoint = options.DefaultHardLimitPerEndpoint <= 0 ? defaults.DefaultHardLimitPerEndpoint : options.DefaultHardLimitPerEndpoint,
            MaxScenarioBudgetPerBatch = options.MaxScenarioBudgetPerBatch <= 0 ? defaults.MaxScenarioBudgetPerBatch : options.MaxScenarioBudgetPerBatch,
        };
    }

    private static string ResolveHttpMethod(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        return (endpoint?.HttpMethod ?? metadata?.HttpMethod ?? string.Empty).Trim().ToUpperInvariant();
    }

    private static bool IsSimpleMethod(string method)
    {
        return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasRequestBody(ApiEndpointMetadataDto metadata)
    {
        return metadata?.HasRequiredRequestBody == true
            || metadata?.Parameters?.Any(x =>
                string.Equals(x.Location, "Body", StringComparison.OrdinalIgnoreCase) &&
                (!string.IsNullOrWhiteSpace(x.Schema) || x.IsRequired)) == true;
    }

    private static bool HasRequestSchema(ApiEndpointMetadataDto metadata)
    {
        return metadata?.ParameterSchemaPayloads?.Any(x => !string.IsNullOrWhiteSpace(x)) == true
            || metadata?.ParameterSchemaRefs?.Any(x => !string.IsNullOrWhiteSpace(x)) == true
            || metadata?.Parameters?.Any(x => !string.IsNullOrWhiteSpace(x.Schema)) == true;
    }

    private static int CountPathQueryParameters(ApiOrderItemModel endpoint, ApiEndpointMetadataDto metadata)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddRange(names, metadata?.RequiredPathParameterNames);
        AddRange(names, metadata?.RequiredQueryParameterNames);

        foreach (var parameter in metadata?.Parameters ?? Array.Empty<ApiEndpointParameterDescriptorDto>())
        {
            if (string.IsNullOrWhiteSpace(parameter?.Name))
            {
                continue;
            }

            if (string.Equals(parameter.Location, "Path", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(parameter.Location, "Query", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(parameter.Name);
            }
        }

        foreach (var pathParameter in ParsePathParameters(endpoint?.Path ?? metadata?.Path))
        {
            names.Add(pathParameter);
        }

        return names.Count;
    }

    private static int CountRequiredFields(ApiEndpointMetadataDto metadata)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameter in metadata?.Parameters ?? Array.Empty<ApiEndpointParameterDescriptorDto>())
        {
            if (parameter == null)
            {
                continue;
            }

            if (parameter.IsRequired && !string.IsNullOrWhiteSpace(parameter.Name) &&
                !string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase))
            {
                names.Add(parameter.Name);
            }

            AddRequiredFieldsFromSchema(names, parameter.Schema);
        }

        foreach (var schema in metadata?.ParameterSchemaPayloads ?? Array.Empty<string>())
        {
            AddRequiredFieldsFromSchema(names, schema);
        }

        return names.Count;
    }

    private static void AddRange(HashSet<string> names, IEnumerable<string> values)
    {
        if (values == null)
        {
            return;
        }

        foreach (var value in values.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            names.Add(value);
        }
    }

    private static void AddRequiredFieldsFromSchema(HashSet<string> names, string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(schema);
            AddRequiredFieldsFromElement(names, document.RootElement);
        }
        catch (JsonException)
        {
        }
    }

    private static void AddRequiredFieldsFromElement(HashSet<string> names, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("required", out var required) &&
            required.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in required.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var name = item.GetString();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        names.Add(name);
                    }
                }
            }
        }

        foreach (var propertyName in new[] { "properties", "items", "allOf", "oneOf", "anyOf" })
        {
            if (!element.TryGetProperty(propertyName, out var child))
            {
                continue;
            }

            if (child.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in child.EnumerateObject())
                {
                    AddRequiredFieldsFromElement(names, property.Value);
                }
            }
            else if (child.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in child.EnumerateArray())
                {
                    AddRequiredFieldsFromElement(names, item);
                }
            }
        }
    }

    private static IReadOnlyList<string> ParsePathParameters(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<string>();
        }

        var parameters = new List<string>();
        var startIndex = 0;

        while ((startIndex = path.IndexOf("{", startIndex, StringComparison.Ordinal)) >= 0)
        {
            var endIndex = path.IndexOf('}', startIndex);
            if (endIndex < 0)
            {
                break;
            }

            var name = path.Substring(startIndex + 1, endIndex - startIndex - 1).Trim();
            if (!string.IsNullOrWhiteSpace(name))
            {
                parameters.Add(name);
            }

            startIndex = endIndex + 1;
        }

        return parameters;
    }
}
