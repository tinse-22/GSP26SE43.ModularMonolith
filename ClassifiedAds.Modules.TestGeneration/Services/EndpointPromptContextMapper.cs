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
                ResponseBodySchema = ep.ResponseSchemaPayloads?.FirstOrDefault(),
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

        if (ep.ResponseSchemaPayloads != null)
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
