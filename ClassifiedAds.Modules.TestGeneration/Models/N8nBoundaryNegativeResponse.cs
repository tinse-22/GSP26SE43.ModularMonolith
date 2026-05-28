using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Response from n8n webhook for boundary/negative test scenario generation.
/// </summary>
public class N8nBoundaryNegativeResponse
{
    public List<N8nSuggestedScenario> Scenarios { get; set; } = new();

    public string Model { get; set; }

    public int? TokensUsed { get; set; }
}

public class N8nSuggestedScenario
{
    public string ScenarioKey { get; set; }

    public Guid EndpointId { get; set; }

    public string ScenarioName { get; set; }

    public string Description { get; set; }

    /// <summary>"Boundary" or "Negative"</summary>
    public string TestType { get; set; }

    public string Priority { get; set; }

    public List<string> Tags { get; set; } = new();

    /// <summary>Reuse existing N8nTestCaseRequest model from happy-path.</summary>
    public N8nTestCaseRequest Request { get; set; }

    /// <summary>Reuse existing N8nTestCaseExpectation model from happy-path.</summary>
    public N8nTestCaseExpectation Expectation { get; set; }

    public List<N8nTestCaseVariable> Variables { get; set; } = new();

    /// <summary>
    /// SRS requirement codes (e.g. "REQ-001") this scenario covers, as output by the LLM.
    /// Mapped to GUIDs by BE during parsing.
    /// </summary>
    public List<string> CoveredRequirementCodes { get; set; } = new();

    /// <summary>
    /// Optional credential rewrite intent from n8n.
    /// Supported values: preserve, rewrite_email, rewrite_password, rewrite_both.
    /// </summary>
    public string CredentialPolicy { get; set; }

    /// <summary>
    /// Optional list of request fields that must never be rewritten by BE resolver.
    /// Example: ["request.body.password", "request.body.email"].
    /// </summary>
    public List<string> LockedFields { get; set; } = new();

    /// <summary>
    /// Optional auth mode hint from n8n.
    /// Supported values: none, optional, required.
    /// </summary>
    public string AuthMode { get; set; }

    /// <summary>
    /// Optional structured execution hints to keep callback extensible.
    /// </summary>
    public N8nExecutionHints ExecutionHints { get; set; }
}
