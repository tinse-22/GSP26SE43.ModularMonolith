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
            return Array.Empty<EndpointPromptContext>();

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
        // Extract type info from schema payloads if available
        var result = new List<ParameterPromptContext>();

        if (ep.ParameterSchemaPayloads != null)
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
            return null;

        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(globalRules))
            parts.Add($"[Global Rules] {globalRules.Trim()}");
        if (!string.IsNullOrWhiteSpace(endpointContext))
            parts.Add($"[Endpoint-specific] {endpointContext.Trim()}");

        return string.Join("\n\n", parts);
    }
}
