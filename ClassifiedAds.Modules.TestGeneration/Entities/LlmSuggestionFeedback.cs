using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

public class LlmSuggestionFeedback : Entity<Guid>, IAggregateRoot
{
    public Guid SuggestionId { get; set; }

    public Guid TestSuiteId { get; set; }

    public Guid? EndpointId { get; set; }

    public Guid UserId { get; set; }

    public LlmSuggestionFeedbackSignal FeedbackSignal { get; set; }

    public string Notes { get; set; }

    public LlmSuggestion Suggestion { get; set; }
}

public enum LlmSuggestionFeedbackSignal
{
    Helpful = 0,
    NotHelpful = 1,
}
