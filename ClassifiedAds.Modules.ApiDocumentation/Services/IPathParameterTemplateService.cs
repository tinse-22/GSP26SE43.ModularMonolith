using ClassifiedAds.Modules.ApiDocumentation.Models;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Services;

/// <summary>
/// Service for path parameter template operations: extraction, validation, resolution, and mutation generation.
/// </summary>
public interface IPathParameterTemplateService
{
    /// <summary>
    /// Extract path parameters from a URL template containing {placeholders}.
    /// </summary>
    /// <param name="pathTemplate">URL template path, e.g. /api/users/{userId}/posts/{postId}</param>
    /// <returns>List of extracted path parameter info in order of appearance</returns>
    List<PathParameterInfo> ExtractPathParameters(string pathTemplate);

    /// <summary>
    /// Validate consistency between path template placeholders and defined parameters.
    /// </summary>
    /// <param name="path">Path template with {placeholders}</param>
    /// <param name="parameters">List of defined parameters from user input</param>
    /// <returns>Validation result with errors, auto-created params, and warnings</returns>
    PathTemplateValidationResult ValidatePathParameterConsistency(string path, List<ManualParameterDefinition> parameters);

    /// <summary>
    /// Ensure path parameter consistency: validate and auto-create missing path parameters.
    /// Throws ValidationException if inconsistencies cannot be resolved.
    /// </summary>
    /// <param name="path">Path template with {placeholders}</param>
    /// <param name="parameters">List of defined parameters from user input</param>
    /// <returns>Merged list of parameters (original + auto-created)</returns>
    List<ManualParameterDefinition> EnsurePathParameterConsistency(string path, List<ManualParameterDefinition> parameters);

    /// <summary>
    /// Resolve a path template by replacing {placeholders} with provided values.
    /// Values are URI-encoded. ResolvedUrl is null when any placeholders are unresolved.
    /// </summary>
    /// <param name="path">Path template with {placeholders}</param>
    /// <param name="parameterValues">Dictionary of parameter name → sample value</param>
    /// <returns>Resolution result with resolved URL and unresolved params</returns>
    ResolvedUrlResult ResolveUrl(string path, Dictionary<string, string> parameterValues);

    /// <summary>
    /// Generate mutation variants for a path parameter (for negative/boundary testing).
    /// </summary>
    /// <param name="parameterName">Name of the path parameter</param>
    /// <param name="dataType">Data type: string, integer, number, boolean, uuid</param>
    /// <param name="format">Optional format: int32, int64, uuid, email, etc.</param>
    /// <param name="defaultValue">Current sample value</param>
    /// <returns>List of mutation variants for test generation</returns>
    List<PathParameterMutation> GenerateMutations(string parameterName, string dataType, string format, string defaultValue);
}
