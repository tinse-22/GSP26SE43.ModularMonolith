using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

public class ReorderApiTestOrderRequest
{
    [Required]
    public string RowVersion { get; set; }

    [Required]
    public List<Guid> OrderedEndpointIds { get; set; } = new();

    [MaxLength(4000)]
    public string ReviewNotes { get; set; }
}
