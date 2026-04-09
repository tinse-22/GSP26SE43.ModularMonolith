using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

/// <summary>
/// Request body for reviewing (approve/reject/modify) an LLM suggestion (FE-15).
/// </summary>
public class ReviewLlmSuggestionRequest
{
    /// <summary>
    /// Review action: "Approve", "Reject", or "Modify".
    /// </summary>
    public string Action { get; set; }

    /// <summary>
    /// Base64-encoded RowVersion for optimistic concurrency.
    /// </summary>
    public string RowVersion { get; set; }

    /// <summary>
    /// Optional review notes (required for Reject).
    /// </summary>
    public string ReviewNotes { get; set; }

    /// <summary>
    /// Modified content (required when Action is "Modify").
    /// </summary>
    public EditableLlmSuggestionInput ModifiedContent { get; set; }
}

/// <summary>
/// User-editable fields when modifying a suggestion before approving.
/// </summary>
public class EditableLlmSuggestionInput
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string TestType { get; set; }
    public string Priority { get; set; }
    public List<string> Tags { get; set; }
    public EditableSuggestionRequestInput Request { get; set; }
    public EditableSuggestionExpectationInput Expectation { get; set; }
    public List<EditableSuggestionVariableInput> Variables { get; set; }
}

public class EditableSuggestionRequestInput
{
    public string HttpMethod { get; set; }
    public string Url { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public Dictionary<string, string> PathParams { get; set; }
    public Dictionary<string, string> QueryParams { get; set; }
    public string Body { get; set; }
}

public class EditableSuggestionExpectationInput
{
    public List<int> ExpectedStatus { get; set; }
    public List<string> BodyContains { get; set; }
    public List<string> BodyNotContains { get; set; }
    public string ResponseSchema { get; set; }
    public Dictionary<string, string> HeaderChecks { get; set; }
    public Dictionary<string, string> JsonPathChecks { get; set; }
    public int? MaxResponseTime { get; set; }
}

public class EditableSuggestionVariableInput
{
    public string VariableName { get; set; }
    public string ExtractFrom { get; set; }
    public string JsonPath { get; set; }
    public string HeaderName { get; set; }
    public string Regex { get; set; }
    public string DefaultValue { get; set; }
}
