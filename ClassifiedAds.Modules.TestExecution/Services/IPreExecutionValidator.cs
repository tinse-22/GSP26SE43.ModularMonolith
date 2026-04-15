using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Services;

public interface IPreExecutionValidator
{
    /// <summary>
    /// Validates a test case request for completeness before execution.
    /// Returns all issues found (not just the first one) with actionable fix suggestions.
    /// </summary>
    PreExecutionValidationResult Validate(
        ExecutionTestCaseDto testCase,
        ResolvedExecutionEnvironment environment,
        IReadOnlyDictionary<string, string> variableBag,
        ApiEndpointMetadataDto endpointMetadata);
}
