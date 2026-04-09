using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

public class UpsertLlmSuggestionFeedbackRequest
{
    [Required]
    [MaxLength(20)]
    public string Signal { get; set; }

    [MaxLength(4000)]
    public string Notes { get; set; }
}
