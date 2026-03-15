using ClassifiedAds.Contracts.TestExecution.DTOs;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public interface IFailureExplanationSanitizer
{
    TestFailureExplanationContextDto Sanitize(TestFailureExplanationContextDto context);
}
