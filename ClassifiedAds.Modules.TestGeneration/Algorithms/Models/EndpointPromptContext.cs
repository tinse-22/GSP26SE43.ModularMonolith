using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Algorithms.Models;

/// <summary>
/// Context for building Observation-Confirmation prompts for LLM-based test expectation generation.
/// Source: COmbine/RBCTest paper (arXiv:2504.17287) - Observation-Confirmation prompting pattern.
/// </summary>
public class EndpointPromptContext
{
    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public string OperationId { get; set; }

    public string Summary { get; set; }

    public string Description { get; set; }

    public List<ParameterPromptContext> Parameters { get; set; } = new();

    public List<ResponsePromptContext> Responses { get; set; } = new();

    /// <summary>
    /// Raw JSON schema of the request body (if applicable).
    /// </summary>
    public string RequestBodySchema { get; set; }

    /// <summary>
    /// Raw JSON schema of the primary success response body.
    /// </summary>
    public string ResponseBodySchema { get; set; }

    /// <summary>
    /// Example request body from OAS spec (if available).
    /// Used in Confirmation phase to cross-check constraints.
    /// </summary>
    public string RequestExample { get; set; }

    /// <summary>
    /// Example response body from OAS spec (if available).
    /// Used in Confirmation phase to cross-check constraints.
    /// </summary>
    public string ResponseExample { get; set; }

    /// <summary>
    /// User-provided business rules for this endpoint (plain text, optional).
    /// Provides domain-specific context that is not captured in OAS spec.
    /// Example: "Only allow registration when user >= 17 years old"
    /// </summary>
    public string BusinessContext { get; set; }
}

public class ParameterPromptContext
{
    public string Name { get; set; }

    /// <summary>
    /// Location: path, query, header, cookie.
    /// </summary>
    public string In { get; set; }

    public bool Required { get; set; }

    public string Schema { get; set; }

    public string Description { get; set; }
}

public class ResponsePromptContext
{
    public int StatusCode { get; set; }

    public string Description { get; set; }

    public string Schema { get; set; }
}

/// <summary>
/// Output of the Observation-Confirmation prompt builder.
/// Contains the two-phase prompt that reduces LLM hallucination.
/// </summary>
public class ObservationConfirmationPrompt
{
    /// <summary>
    /// Phase 1: Ask LLM to list all observed constraints from the OAS spec.
    /// </summary>
    public string ObservationPrompt { get; set; }

    /// <summary>
    /// Phase 2: Ask LLM to confirm each constraint with evidence from the spec.
    /// Only confirmed constraints become test expectations.
    /// </summary>
    public string ConfirmationPromptTemplate { get; set; }

    /// <summary>
    /// Combined single-shot prompt (for models that don't support multi-turn).
    /// Uses Chain-of-Thought with observation then confirmation steps.
    /// </summary>
    public string CombinedPrompt { get; set; }

    /// <summary>
    /// System prompt to set the LLM's role and output format.
    /// </summary>
    public string SystemPrompt { get; set; }
}
