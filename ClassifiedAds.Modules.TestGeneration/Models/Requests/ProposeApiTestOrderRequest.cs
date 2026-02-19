using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

public class ProposeApiTestOrderRequest
{
    [Required]
    public Guid SpecificationId { get; set; }

    public List<Guid> SelectedEndpointIds { get; set; } = new();

    public ProposalSource Source { get; set; } = ProposalSource.Ai;

    [MaxLength(100)]
    public string LlmModel { get; set; }

    [MaxLength(2000)]
    public string ReasoningNote { get; set; }
}
