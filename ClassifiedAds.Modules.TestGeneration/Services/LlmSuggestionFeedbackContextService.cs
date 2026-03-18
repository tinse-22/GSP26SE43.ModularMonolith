using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public class LlmSuggestionFeedbackContextService : ILlmSuggestionFeedbackContextService
{
    private readonly IRepository<LlmSuggestionFeedback, Guid> _feedbackRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;
    private readonly LlmSuggestionFeedbackMetrics _metrics;

    public LlmSuggestionFeedbackContextService(
        IRepository<LlmSuggestionFeedback, Guid> feedbackRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository,
        LlmSuggestionFeedbackMetrics metrics)
    {
        _feedbackRepository = feedbackRepository;
        _suggestionRepository = suggestionRepository;
        _metrics = metrics;
    }

    public async Task<LlmSuggestionFeedbackContextResult> BuildAsync(
        Guid testSuiteId,
        IReadOnlyCollection<Guid> endpointIds,
        CancellationToken cancellationToken = default)
    {
        _metrics.RecordContextBuild();

        if (testSuiteId == Guid.Empty || endpointIds == null || endpointIds.Count == 0)
        {
            return LlmSuggestionFeedbackContextResult.Empty;
        }

        try
        {
            var requestedEndpointIds = endpointIds
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToArray();

            if (requestedEndpointIds.Length == 0)
            {
                return LlmSuggestionFeedbackContextResult.Empty;
            }

            var feedbackRows = await _feedbackRepository.ToListAsync(
                from feedback in _feedbackRepository.GetQueryableSet()
                join suggestion in _suggestionRepository.GetQueryableSet()
                    on feedback.SuggestionId equals suggestion.Id
                where feedback.TestSuiteId == testSuiteId
                    && suggestion.TestSuiteId == testSuiteId
                    && feedback.EndpointId.HasValue
                    && suggestion.EndpointId.HasValue
                    && feedback.EndpointId == suggestion.EndpointId
                    && requestedEndpointIds.Contains(feedback.EndpointId.Value)
                    && suggestion.ReviewStatus != ReviewStatus.Superseded
                select feedback);

            if (feedbackRows.Count == 0)
            {
                return LlmSuggestionFeedbackContextResult.Empty;
            }

            var endpointContexts = new Dictionary<Guid, string>();
            var canonicalLines = new List<string>();

            foreach (var endpointGroup in feedbackRows
                .Where(x => x.EndpointId.HasValue)
                .GroupBy(x => x.EndpointId!.Value)
                .OrderBy(x => x.Key))
            {
                var helpfulCount = endpointGroup.Count(x => x.FeedbackSignal == LlmSuggestionFeedbackSignal.Helpful);
                var notHelpfulCount = endpointGroup.Count(x => x.FeedbackSignal == LlmSuggestionFeedbackSignal.NotHelpful);

                var selectedNotes = endpointGroup
                    .OrderByDescending(x => x.UpdatedDateTime ?? x.CreatedDateTime)
                    .ThenBy(x => x.Notes)
                    .Select(x => LlmSuggestionFeedbackTextSanitizer.NormalizeForPrompt(x.Notes))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.Ordinal)
                    .Take(LlmSuggestionFeedbackTextSanitizer.MaxPromptNotesPerEndpoint)
                    .ToList();

                endpointContexts[endpointGroup.Key] = BuildContextText(helpfulCount, notHelpfulCount, selectedNotes);

                canonicalLines.Add(BuildCanonicalLine(
                    endpointGroup.Key,
                    helpfulCount,
                    notHelpfulCount,
                    selectedNotes));
            }

            if (endpointContexts.Count == 0)
            {
                return LlmSuggestionFeedbackContextResult.Empty;
            }

            return new LlmSuggestionFeedbackContextResult
            {
                EndpointFeedbackContexts = endpointContexts,
                FeedbackFingerprint = ComputeFingerprint(canonicalLines),
            };
        }
        catch
        {
            _metrics.RecordContextFailure();
            throw;
        }
    }

    private static string BuildContextText(
        int helpfulCount,
        int notHelpfulCount,
        IReadOnlyCollection<string> selectedNotes)
    {
        var lines = new List<string>
        {
            $"Helpful: {helpfulCount}",
            $"NotHelpful: {notHelpfulCount}",
        };

        foreach (var note in selectedNotes)
        {
            lines.Add($"- {note}");
        }

        return string.Join("\n", lines);
    }

    private static string BuildCanonicalLine(
        Guid endpointId,
        int helpfulCount,
        int notHelpfulCount,
        IReadOnlyCollection<string> selectedNotes)
    {
        var orderedNotes = selectedNotes
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();

        return $"{endpointId:N}|{helpfulCount}|{notHelpfulCount}|{string.Join("||", orderedNotes)}";
    }

    private static string ComputeFingerprint(IEnumerable<string> canonicalLines)
    {
        var canonical = string.Join("\n", canonicalLines);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(hash)[..16];
    }
}
