using ClassifiedAds.Modules.TestGeneration.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Models;

public class LlmSuggestionFeedbackModel
{
    public Guid Id { get; set; }

    public Guid SuggestionId { get; set; }

    public Guid TestSuiteId { get; set; }

    public Guid? EndpointId { get; set; }

    public Guid UserId { get; set; }

    public string Signal { get; set; }

    public string Notes { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }

    public string RowVersion { get; set; }

    public static LlmSuggestionFeedbackModel FromEntity(LlmSuggestionFeedback entity)
    {
        return new LlmSuggestionFeedbackModel
        {
            Id = entity.Id,
            SuggestionId = entity.SuggestionId,
            TestSuiteId = entity.TestSuiteId,
            EndpointId = entity.EndpointId,
            UserId = entity.UserId,
            Signal = entity.FeedbackSignal.ToString(),
            Notes = entity.Notes,
            CreatedDateTime = entity.CreatedDateTime,
            UpdatedDateTime = entity.UpdatedDateTime,
            RowVersion = entity.RowVersion != null ? Convert.ToBase64String(entity.RowVersion) : null,
        };
    }
}
