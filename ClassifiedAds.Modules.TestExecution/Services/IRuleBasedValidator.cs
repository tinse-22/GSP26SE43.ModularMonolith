using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Services;

public interface IRuleBasedValidator
{
    TestCaseValidationResult Validate(
        HttpTestResponse response,
        ExecutionTestCaseDto testCase,
        ApiEndpointMetadataDto endpointMetadata = null,
        ValidationProfile profile = ValidationProfile.Default,
        IReadOnlyDictionary<string, string> variableBag = null);
}
