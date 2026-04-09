using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Rule-based engine for generating request body mutations.
/// Used for boundary/negative test case generation (FE-06).
/// </summary>
public interface IBodyMutationEngine
{
    /// <summary>
    /// Generate body mutation variants from parameter details and schema.
    /// </summary>
    /// <returns></returns>
    IReadOnlyList<BodyMutation> GenerateMutations(BodyMutationContext context);
}

/// <summary>
/// Context for body mutation generation.
/// </summary>
public class BodyMutationContext
{
    public Guid EndpointId { get; set; }

    public string HttpMethod { get; set; }

    public string Path { get; set; }

    /// <summary>
    /// Body parameters from IApiEndpointParameterDetailService (Location == "Body").
    /// </summary>
    public IReadOnlyList<ParameterDetailDto> BodyParameters { get; set; } = Array.Empty<ParameterDetailDto>();

    /// <summary>
    /// Raw JSON schema from ParameterSchemaPayloads for the request body.
    /// </summary>
    public string RequestBodySchema { get; set; }
}
