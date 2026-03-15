using ClassifiedAds.Modules.LlmAssistant.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public interface ILlmFailureExplanationClient
{
    Task<FailureExplanationProviderResponse> ExplainAsync(
        FailureExplanationPrompt prompt,
        CancellationToken ct = default);
}
