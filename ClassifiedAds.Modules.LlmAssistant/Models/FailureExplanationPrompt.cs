using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestExecution.DTOs;

namespace ClassifiedAds.Modules.LlmAssistant.Models;

public class FailureExplanationPrompt
{
    public string Provider { get; set; }

    public string Model { get; set; }

    public string Prompt { get; set; }

    public string SanitizedContextJson { get; set; }

    public TestFailureExplanationContextDto SanitizedContext { get; set; }

    public ApiEndpointMetadataDto EndpointMetadata { get; set; }
}
