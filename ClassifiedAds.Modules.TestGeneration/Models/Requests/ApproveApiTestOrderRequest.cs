using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

public class ApproveApiTestOrderRequest
{
    [Required]
    public string RowVersion { get; set; }

    [MaxLength(4000)]
    public string ReviewNotes { get; set; }
}
