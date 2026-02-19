using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

public class RejectApiTestOrderRequest
{
    [Required]
    public string RowVersion { get; set; }

    [Required]
    [MaxLength(4000)]
    public string ReviewNotes { get; set; }
}
