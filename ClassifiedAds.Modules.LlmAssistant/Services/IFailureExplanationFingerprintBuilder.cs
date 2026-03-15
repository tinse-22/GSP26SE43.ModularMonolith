using ClassifiedAds.Contracts.TestExecution.DTOs;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public interface IFailureExplanationFingerprintBuilder
{
    string Build(TestFailureExplanationContextDto context);
}
