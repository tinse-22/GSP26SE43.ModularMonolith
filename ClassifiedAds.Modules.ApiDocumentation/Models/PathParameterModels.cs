using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

/// <summary>
/// Information about a path parameter extracted from URL template.
/// </summary>
public class PathParameterInfo
{
    /// <summary>
    /// Parameter name from {placeholder}.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 0-based position in path (order of appearance).
    /// </summary>
    public int Position { get; set; }
}

/// <summary>
/// Result of path parameter consistency validation.
/// </summary>
public class PathTemplateValidationResult
{
    public bool IsValid { get; set; }

    public List<string> Errors { get; set; } = new();

    public List<ManualParameterDefinition> AutoCreatedParams { get; set; } = new();

    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Result of path parameter URL resolution.
/// </summary>
public class ResolvedUrlResult
{
    /// <summary>Original path template with {placeholders}.</summary>
    public string OriginalTemplate { get; set; } = string.Empty;

    /// <summary>Path with placeholders replaced by actual values. Null when unresolved params exist.</summary>
    public string ResolvedUrl { get; set; }

    /// <summary>Dictionary of successfully resolved params (name → value).</summary>
    public Dictionary<string, string> ResolvedParameters { get; set; } = new();

    /// <summary>List of param names that could not be resolved (no value provided).</summary>
    public List<string> UnresolvedParameters { get; set; } = new();

    /// <summary>True when all path placeholders were resolved.</summary>
    public bool IsFullyResolved { get; set; }
}

/// <summary>
/// Represents a single mutation variant for a path parameter.
/// Used for negative testing and boundary testing in test generation.
/// </summary>
public class PathParameterMutation
{
    /// <summary>Mutation type identifier: empty, wrongType, boundary_zero, nonExistent, etc.</summary>
    public string MutationType { get; set; } = string.Empty;

    /// <summary>Human-readable label: '{paramName} — {mutation description}'.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>The mutated value to substitute into the URL.</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Expected HTTP status code when using this mutated value (400, 404, etc.).</summary>
    public int ExpectedStatusCode { get; set; }

    /// <summary>Vietnamese description of what this mutation tests.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Mutation variant with resolved URL for direct test execution.
/// </summary>
public class PathParameterMutationWithUrl : PathParameterMutation
{
    /// <summary>
    /// URL resolved with the mutated value for this param (other params use DefaultValue).
    /// Can be null when not all placeholders are resolvable.
    /// </summary>
    public string ResolvedUrl { get; set; }
}

/// <summary>
/// Group of mutations for a single path parameter.
/// </summary>
public class ParameterMutationGroup
{
    public string ParameterName { get; set; } = string.Empty;

    public string DataType { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string OriginalValue { get; set; } = string.Empty;

    public List<PathParameterMutationWithUrl> Mutations { get; set; } = new();
}

/// <summary>
/// Result containing all mutation variants for path parameters of an endpoint.
/// </summary>
public class PathParamMutationsResult
{
    public Guid EndpointId { get; set; }

    public string TemplatePath { get; set; } = string.Empty;

    public int TotalMutations { get; set; }

    public List<ParameterMutationGroup> ParameterMutations { get; set; } = new();
}
