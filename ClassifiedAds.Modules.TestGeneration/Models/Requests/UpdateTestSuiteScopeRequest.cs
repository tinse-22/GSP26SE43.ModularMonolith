using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

public class UpdateTestSuiteScopeRequest
{
    [Required]
    public string RowVersion { get; set; }

    [Required]
    [MaxLength(200)]
    public string Name { get; set; }

    [MaxLength(4000)]
    public string Description { get; set; }

    [Required]
    public Guid ApiSpecId { get; set; }

    public GenerationType GenerationType { get; set; } = GenerationType.Auto;

    [Required]
    [MinLength(1)]
    public List<Guid> SelectedEndpointIds { get; set; } = new();
}
