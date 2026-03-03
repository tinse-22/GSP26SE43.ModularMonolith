using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using System.Collections.Generic;

namespace ClassifiedAds.Contracts.ApiDocumentation.Services;

/// <summary>
/// Cross-module contract for generating path parameter mutation variants.
/// Wraps PathParameterTemplateService.GenerateMutations() from ApiDocumentation module.
/// Used by BoundaryNegativeTestCaseGenerator (FE-06).
/// </summary>
public interface IPathParameterMutationGatewayService
{
    /// <summary>
    /// Generate mutation variants for a path parameter (empty, wrongType, boundary, injection, etc.).
    /// </summary>
    /// <param name="parameterName">Name of the path parameter.</param>
    /// <param name="dataType">Data type: string, integer, number, boolean.</param>
    /// <param name="format">Optional format: int32, int64, uuid, email, etc.</param>
    /// <param name="defaultValue">Current sample value.</param>
    /// <returns>List of mutation variants for test generation.</returns>
    IReadOnlyList<PathParameterMutationDto> GenerateMutations(
        string parameterName, string dataType, string format, string defaultValue);
}
