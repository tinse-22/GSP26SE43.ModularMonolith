using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Maps <see cref="ApiEndpointMetadataDto"/> + business context
/// to <see cref="EndpointPromptContext"/> for prompt generation.
/// </summary>
public static class EndpointPromptContextMapper
{
    public static IReadOnlyList<EndpointPromptContext> Map(
        IReadOnlyList<ApiEndpointMetadataDto> endpoints,
        TestSuite suite)
    {
        if (endpoints == null || endpoints.Count == 0)
        {
            return Array.Empty<EndpointPromptContext>();
        }

        var businessContexts = suite?.EndpointBusinessContexts ?? new Dictionary<Guid, string>();

        return endpoints.Select(ep =>
        {
            businessContexts.TryGetValue(ep.EndpointId, out var businessContext);

            return new EndpointPromptContext
            {
                HttpMethod = ep.HttpMethod,
                Path = ep.Path,
                OperationId = ep.OperationId,
                RequestBodySchema = ep.ParameterSchemaPayloads?.FirstOrDefault(),
                ResponseBodySchema = ResolvePrimarySuccessResponseSchema(ep),
                RequestExample = ResolvePrimaryRequestExample(ep),
                ResponseExample = ResolvePrimarySuccessResponseExample(ep),
                Parameters = MapParameters(ep),
                Responses = MapResponses(ep),
                BusinessContext = CombineBusinessContext(businessContext, suite?.GlobalBusinessRules),
            };
        }).ToList();
    }

    private static List<ParameterPromptContext> MapParameters(ApiEndpointMetadataDto ep)
    {
        var result = new List<ParameterPromptContext>();

        if (ep?.Parameters != null && ep.Parameters.Count > 0)
        {
            foreach (var parameter in ep.Parameters)
            {
                if (string.IsNullOrWhiteSpace(parameter?.Name))
                {
                    continue;
                }

                var schema = !string.IsNullOrWhiteSpace(parameter.Schema)
                    ? parameter.Schema
                    : BuildSimpleSchema(parameter.DataType, parameter.Format, parameter.DefaultValue, parameter.Examples);

                var details = new List<string>();
                if (!string.IsNullOrWhiteSpace(parameter.DataType))
                {
                    details.Add($"type={parameter.DataType}");
                }

                if (!string.IsNullOrWhiteSpace(parameter.Format))
                {
                    details.Add($"format={parameter.Format}");
                }

                if (!string.IsNullOrWhiteSpace(parameter.DefaultValue))
                {
                    details.Add($"default={parameter.DefaultValue}");
                }

                if (!string.IsNullOrWhiteSpace(parameter.Examples))
                {
                    details.Add($"examples={parameter.Examples}");
                }

                result.Add(new ParameterPromptContext
                {
                    Name = parameter.Name,
                    In = (parameter.Location ?? string.Empty).Trim().ToLowerInvariant(),
                    Required = parameter.IsRequired,
                    Schema = schema,
                    Description = details.Count == 0 ? null : string.Join("; ", details),
                });
            }

            return result;
        }

        if (ep?.ParameterSchemaPayloads != null)
        {
            int index = 0;
            foreach (var schema in ep.ParameterSchemaPayloads)
            {
                result.Add(new ParameterPromptContext
                {
                    Name = $"param_{index}",
                    In = "body",
                    Required = true,
                    Schema = schema,
                });
                index++;
            }
        }

        return result;
    }

    private static string BuildSimpleSchema(string dataType, string format, string defaultValue, string examples)
    {
        var fragments = new List<string>();

        if (!string.IsNullOrWhiteSpace(dataType))
        {
            fragments.Add($"\"type\":\"{dataType}\"");
        }

        if (!string.IsNullOrWhiteSpace(format))
        {
            fragments.Add($"\"format\":\"{format}\"");
        }

        if (!string.IsNullOrWhiteSpace(defaultValue))
        {
            fragments.Add($"\"default\":\"{defaultValue}\"");
        }

        if (!string.IsNullOrWhiteSpace(examples))
        {
            fragments.Add($"\"examples\":{examples}");
        }

        if (fragments.Count == 0)
        {
            return null;
        }

        return "{" + string.Join(",", fragments) + "}";
    }

    private static List<ResponsePromptContext> MapResponses(ApiEndpointMetadataDto ep)
    {
        var result = new List<ResponsePromptContext>();

        if (ep?.Responses != null && ep.Responses.Count > 0)
        {
            foreach (var group in ep.Responses
                .Where(response => response != null && response.StatusCode > 0)
                .GroupBy(response => response.StatusCode)
                .OrderBy(group => group.Key))
            {
                var primary = group.FirstOrDefault(response => !string.IsNullOrWhiteSpace(response.Schema))
                    ?? group.First();

                result.Add(new ResponsePromptContext
                {
                    StatusCode = group.Key,
                    Description = string.IsNullOrWhiteSpace(primary.Description)
                        ? BuildDefaultResponseDescription(group.Key)
                        : primary.Description,
                    Schema = primary.Schema,
                });
            }

            return result;
        }

        if (ep?.ResponseSchemaPayloads != null)
        {
            foreach (var schema in ep.ResponseSchemaPayloads)
            {
                result.Add(new ResponsePromptContext
                {
                    StatusCode = 200,
                    Description = "Success response",
                    Schema = schema,
                });
            }
        }

        return result;
    }

    private static string ResolvePrimarySuccessResponseSchema(ApiEndpointMetadataDto endpoint)
    {
        var responseSchema = endpoint?.Responses?
            .Where(response => response is { StatusCode: >= 200 and < 300 } && !string.IsNullOrWhiteSpace(response.Schema))
            .OrderBy(response => response.StatusCode)
            .Select(response => response.Schema)
            .FirstOrDefault();

        return !string.IsNullOrWhiteSpace(responseSchema)
            ? responseSchema
            : endpoint?.ResponseSchemaPayloads?.FirstOrDefault();
    }

    private static string ResolvePrimarySuccessResponseExample(ApiEndpointMetadataDto endpoint)
    {
        return endpoint?.Responses?
            .Where(response => response is { StatusCode: >= 200 and < 300 } && !string.IsNullOrWhiteSpace(response.Examples))
            .OrderBy(response => response.StatusCode)
            .Select(response => response.Examples)
            .FirstOrDefault();
    }

    private static string ResolvePrimaryRequestExample(ApiEndpointMetadataDto endpoint)
    {
        return endpoint?.Parameters?
            .Where(parameter => parameter != null
                && string.Equals(parameter.Location, "Body", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(parameter.Examples))
            .Select(parameter => parameter.Examples)
            .FirstOrDefault();
    }

    private static string BuildDefaultResponseDescription(int statusCode)
    {
        return statusCode switch
        {
            >= 200 and < 300 => "Success response",
            >= 400 and < 500 => "Client error response",
            >= 500 and < 600 => "Server error response",
            _ => "Response",
        };
    }

    private static string CombineBusinessContext(string endpointContext, string globalRules)
    {
        if (string.IsNullOrWhiteSpace(endpointContext) && string.IsNullOrWhiteSpace(globalRules))
        {
            return null;
        }

        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(globalRules))
        {
            parts.Add($"[Global Rules] {globalRules.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(endpointContext))
        {
            parts.Add($"[Endpoint-specific] {endpointContext.Trim()}");
        }

        return string.Join("\n\n", parts);
    }
}
