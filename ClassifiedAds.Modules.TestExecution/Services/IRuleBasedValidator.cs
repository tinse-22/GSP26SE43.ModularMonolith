using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;

namespace ClassifiedAds.Modules.TestExecution.Services;

public interface IRuleBasedValidator
{
    TestCaseValidationResult Validate(
        HttpTestResponse response,
        ExecutionTestCaseDto testCase,
        ApiEndpointMetadataDto endpointMetadata = null,
        ValidationProfile profile = ValidationProfile.Default);
}
