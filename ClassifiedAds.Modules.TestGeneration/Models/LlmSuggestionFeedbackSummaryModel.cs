using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.TestGeneration.Models;

public class LlmSuggestionFeedbackSummaryModel
{
    public static LlmSuggestionFeedbackSummaryModel Empty { get; } = new ();

    public int HelpfulCount { get; set; }

    public int NotHelpfulCount { get; set; }

    public DateTimeOffset? LastFeedbackAt { get; set; }

    public static LlmSuggestionFeedbackSummaryModel FromEntities(IReadOnlyCollection<LlmSuggestionFeedback> feedbacks)
    {
        if (feedbacks == null || feedbacks.Count == 0)
        {
            return new LlmSuggestionFeedbackSummaryModel();
        }

        return new LlmSuggestionFeedbackSummaryModel
        {
            HelpfulCount = feedbacks.Count(x => x.FeedbackSignal == LlmSuggestionFeedbackSignal.Helpful),
            NotHelpfulCount = feedbacks.Count(x => x.FeedbackSignal == LlmSuggestionFeedbackSignal.NotHelpful),
            LastFeedbackAt = feedbacks
                .Select(x => x.UpdatedDateTime ?? x.CreatedDateTime)
                .OrderByDescending(x => x)
                .FirstOrDefault(),
        };
    }
}
