using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

public enum ExpectationSource
{
    Srs = 0,
    Swagger = 1,
    Llm = 2,
    Default = 3,
}

public sealed class ResolvedExpectation
{
    public List<int> ExpectedStatusCodes { get; init; } = new();

    public string ResponseSchema { get; init; }

    public Dictionary<string, string> HeaderChecks { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public List<string> BodyContains { get; init; } = new();

    public List<string> BodyNotContains { get; init; } = new();

    public Dictionary<string, string> JsonPathChecks { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public int? MaxResponseTime { get; init; }

    public ExpectationSource Source { get; init; } = ExpectationSource.Default;

    public Guid? PrimaryRequirementId { get; init; }

    public string RequirementCode { get; init; }
}

public sealed class GeneratedScenarioContext
{
    public Guid? EndpointId { get; init; }

    public TestType TestType { get; init; }

    public string HttpMethod { get; init; }

    public IReadOnlyCollection<ApiEndpointResponseDescriptorDto> SwaggerResponses { get; init; } = Array.Empty<ApiEndpointResponseDescriptorDto>();

    public N8nTestCaseExpectation LlmExpectation { get; init; }

    public IReadOnlyList<SrsRequirement> SrsRequirements { get; init; } = Array.Empty<SrsRequirement>();

    public IReadOnlyList<Guid> CoveredRequirementIds { get; init; } = Array.Empty<Guid>();

    public IReadOnlyList<int> PreferredDefaultStatuses { get; init; } = Array.Empty<int>();

    /// <summary>
    /// Raw SRS document content (markdown). Used by ExpectationResolver to parse
    /// validation rules / constraints directly from the SRS when testableConstraints JSON
    /// on SrsRequirement entities is empty or malformed.
    /// </summary>
    public string SrsDocumentContent { get; init; }

    /// <summary>
    /// When set, the ExpectationResolver will prefer SRS constraints whose field name
    /// matches this value. This prevents applying a generic email constraint to every
    /// body mutation (empty body, missing password, etc.) when only email-specific
    /// mutations should use the email SRS constraint.
    /// </summary>
    public string TargetFieldName { get; init; }
}
