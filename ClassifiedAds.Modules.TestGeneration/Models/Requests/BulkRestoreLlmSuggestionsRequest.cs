using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

/// <summary>
/// Request body for bulk restoring soft-deleted LLM suggestions.
/// </summary>
public class BulkRestoreLlmSuggestionsRequest
{
    [Required]
    [MinLength(1)]
    public List<Guid> SuggestionIds { get; set; }
}
