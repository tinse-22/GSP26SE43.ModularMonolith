using ClassifiedAds.Modules.TestGeneration.Entities;
using System.Diagnostics.Metrics;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public sealed class LlmSuggestionFeedbackMetrics
{
    public const string MeterName = "ClassifiedAds.TestGeneration.LlmSuggestionFeedback";

    private readonly Counter<long> _upsertsTotal;
    private readonly Counter<long> _helpfulTotal;
    private readonly Counter<long> _notHelpfulTotal;
    private readonly Counter<long> _contextBuildTotal;
    private readonly Counter<long> _contextFailuresTotal;

    public LlmSuggestionFeedbackMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _upsertsTotal = meter.CreateCounter<long>(
            "llm_suggestion_feedback_upserts_total",
            description: "Total number of LLM suggestion feedback upserts.");

        _helpfulTotal = meter.CreateCounter<long>(
            "llm_suggestion_feedback_helpful_total",
            description: "Total number of successful Helpful feedback upserts.");

        _notHelpfulTotal = meter.CreateCounter<long>(
            "llm_suggestion_feedback_not_helpful_total",
            description: "Total number of successful NotHelpful feedback upserts.");

        _contextBuildTotal = meter.CreateCounter<long>(
            "llm_suggestion_feedback_context_build_total",
            description: "Total number of feedback-context build attempts.");

        _contextFailuresTotal = meter.CreateCounter<long>(
            "llm_suggestion_feedback_context_failures_total",
            description: "Total number of failed feedback-context builds.");
    }

    public void RecordUpsert(LlmSuggestionFeedbackSignal signal)
    {
        _upsertsTotal.Add(1);

        if (signal == LlmSuggestionFeedbackSignal.Helpful)
        {
            _helpfulTotal.Add(1);
            return;
        }

        _notHelpfulTotal.Add(1);
    }

    public void RecordContextBuild() => _contextBuildTotal.Add(1);

    public void RecordContextFailure() => _contextFailuresTotal.Add(1);
}
