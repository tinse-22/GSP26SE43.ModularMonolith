using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Modules.LlmAssistant.Models;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public interface ILlmFailureExplainer
{
    Task<FailureExplanationModel> ExplainAsync(
        TestFailureExplanationContextDto context,
        ApiEndpointMetadataDto endpointMetadata,
        CancellationToken ct = default);

    Task<FailureExplanationModel> GetCachedAsync(
        TestFailureExplanationContextDto context,
        CancellationToken ct = default);
}
