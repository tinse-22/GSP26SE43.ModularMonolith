using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public interface ILlmSuggestionFeedbackContextService
{
    Task<LlmSuggestionFeedbackContextResult> BuildAsync(
        Guid testSuiteId,
        IReadOnlyCollection<Guid> endpointIds,
        CancellationToken cancellationToken = default);
}

public class LlmSuggestionFeedbackContextResult
{
    public const string EmptyFingerprint = "E3B0C44298FC1C14";

    public static LlmSuggestionFeedbackContextResult Empty { get; } = new()
    {
        EndpointFeedbackContexts = new Dictionary<Guid, string>(),
        FeedbackFingerprint = EmptyFingerprint,
    };

    public IReadOnlyDictionary<Guid, string> EndpointFeedbackContexts { get; init; } = new Dictionary<Guid, string>();

    public string FeedbackFingerprint { get; init; } = EmptyFingerprint;
}
