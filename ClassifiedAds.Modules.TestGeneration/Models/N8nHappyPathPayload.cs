using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Payload sent to n8n webhook for happy-path test case generation.
/// The n8n workflow orchestrates LLM calls using the provided prompts.
/// </summary>
public class N8nHappyPathPayload
{
    public Guid TestSuiteId { get; set; }
    public string TestSuiteName { get; set; }
    public string GlobalBusinessRules { get; set; }
    public List<N8nEndpointPayload> Endpoints { get; set; } = new();
}

public class N8nEndpointPayload
{
    public Guid EndpointId { get; set; }
    public string HttpMethod { get; set; }
    public string Path { get; set; }
    public string OperationId { get; set; }
    public int OrderIndex { get; set; }
    public List<Guid> DependsOnEndpointIds { get; set; } = new();
    public bool IsAuthRelated { get; set; }
    public string BusinessContext { get; set; }

    /// <summary>
    /// Observation-Confirmation prompt for this endpoint (COmbine/RBCTest paper).
    /// </summary>
    public N8nPromptPayload Prompt { get; set; }

    /// <summary>
    /// Raw JSON schemas for request parameters (from OpenAPI spec).
    /// </summary>
    public List<string> ParameterSchemaPayloads { get; set; } = new();

    /// <summary>
    /// Raw JSON schemas for response objects (from OpenAPI spec).
    /// </summary>
    public List<string> ResponseSchemaPayloads { get; set; } = new();
}

public class N8nPromptPayload
{
    public string SystemPrompt { get; set; }
    public string CombinedPrompt { get; set; }
    public string ObservationPrompt { get; set; }
    public string ConfirmationPromptTemplate { get; set; }
}
