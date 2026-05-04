using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// SRS context passed to n8n so the LLM can generate tests that are traceable to requirements.
/// </summary>
public class N8nSrsContext
{
    /// <summary>Title of the SRS document.</summary>
    public string DocumentTitle { get; set; }

    /// <summary>Full SRS content (ParsedMarkdown if available, else RawContent).</summary>
    public string Content { get; set; }

    /// <summary>Structured requirements extracted from the SRS.</summary>
    public List<N8nSrsRequirementBrief> Requirements { get; set; } = new();
}

/// <summary>Brief SRS requirement for LLM Suggestion context (boundary/negative generation).</summary>
public class N8nSrsRequirementBrief
{
    public string Code { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    /// <summary>
    /// Structured testable constraints from SRS analysis. Max 5 items.
    /// Each describes a specific condition the API must enforce.
    /// </summary>
    public List<SrsTestableConstraintBrief> TestableConstraints { get; set; } = new();

    /// <summary>
    /// Endpoint this requirement maps to. LLM must apply constraints to this endpoint only.
    /// Null = applies globally to all endpoints.
    /// </summary>
    public Guid? EndpointId { get; set; }
}

/// <summary>
/// One testable constraint extracted from SRS by LLM analysis.
/// Example: { Constraint = "password must be >= 6 characters", ExpectedOutcome = "400", Priority = "High" }
/// </summary>
public class SrsTestableConstraintBrief
{
    /// <summary>Human-readable constraint (e.g. "password must be >= 6 characters").</summary>
    public string Constraint { get; set; }

    /// <summary>Expected API outcome (e.g. "400" or "201"). Null if not specified.</summary>
    public string ExpectedOutcome { get; set; }

    public string Priority { get; set; }
}

/// <summary>
/// Swagger error response descriptor for a specific HTTP status code.
/// Sent to LLM so it can derive assertions from the API spec instead of guessing.
/// </summary>
public class N8nErrorResponseDescriptor
{
    public string Description { get; set; }
    public string SchemaJson { get; set; }
    public string ExampleJson { get; set; }
}

/// <summary>
/// Payload sent to n8n webhook for boundary/negative test scenario generation.
/// </summary>
public class N8nBoundaryNegativePayload
{
    public Guid TestSuiteId { get; set; }

    public string TestSuiteName { get; set; }

    public string GlobalBusinessRules { get; set; }

    /// <summary>
    /// SRS document context. Null when the test suite has no linked SRS document.
    /// When present, the LLM should align generated scenarios to these requirements.
    /// </summary>
    public N8nSrsContext SrsContext { get; set; }

    public GenerationAlgorithmProfile AlgorithmProfile { get; set; } = new();

    public N8nSuggestionPromptConfig PromptConfig { get; set; } = new();

    public List<N8nBoundaryEndpointPayload> Endpoints { get; set; } = new();
}

public class N8nSuggestionPromptConfig
{
    public string SystemPrompt { get; set; }

    public string TaskInstruction { get; set; }

    public string Rules { get; set; }

    public string ResponseFormat { get; set; }
}

public class N8nBoundaryEndpointPayload
{
    public Guid EndpointId { get; set; }

    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public string OperationId { get; set; }

    public int OrderIndex { get; set; }

    public string BusinessContext { get; set; }

    public string FeedbackContext { get; set; }

    public N8nPromptPayload Prompt { get; set; }

    public List<string> ParameterSchemaPayloads { get; set; } = new();

    public List<string> ResponseSchemaPayloads { get; set; } = new();

    public List<N8nParameterDetail> ParameterDetails { get; set; } = new();

    /// <summary>
    /// Error response descriptors from Swagger (4xx/5xx only).
    /// Key = status code string ("400", "422"). Max 5 entries.
    /// LLM MUST use these codes ONLY in expectedStatus.
    /// </summary>
    public Dictionary<string, N8nErrorResponseDescriptor> ErrorResponses { get; set; } = new();
}

public class N8nParameterDetail
{
    public string Name { get; set; }

    public string Location { get; set; }

    public string DataType { get; set; }

    public string Format { get; set; }

    public bool IsRequired { get; set; }

    public string DefaultValue { get; set; }
}
