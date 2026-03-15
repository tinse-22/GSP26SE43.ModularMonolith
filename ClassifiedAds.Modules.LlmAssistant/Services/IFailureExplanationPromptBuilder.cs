using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Modules.LlmAssistant.Models;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public interface IFailureExplanationPromptBuilder
{
    FailureExplanationPrompt Build(
        TestFailureExplanationContextDto context,
        ApiEndpointMetadataDto endpointMetadata);
}
