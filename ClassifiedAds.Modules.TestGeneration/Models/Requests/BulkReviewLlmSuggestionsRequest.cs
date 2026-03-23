using System;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

/// <summary>
/// Request body for bulk reviewing LLM suggestions.
/// FE-17 supports bulk approve/reject on the current durable suggestion set.
/// </summary>
public class BulkReviewLlmSuggestionsRequest
{
    [Required]
    [MaxLength(20)]
    public string Action { get; set; }

    [MaxLength(50)]
    public string FilterBySuggestionType { get; set; }

    [MaxLength(50)]
    public string FilterByTestType { get; set; }

    public Guid? FilterByEndpointId { get; set; }

    [MaxLength(4000)]
    public string ReviewNotes { get; set; }
}
