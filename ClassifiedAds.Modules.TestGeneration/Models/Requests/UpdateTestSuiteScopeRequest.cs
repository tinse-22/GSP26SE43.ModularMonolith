using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

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

    public Guid? ApiSpecId { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public GenerationType GenerationType { get; set; } = GenerationType.Auto;

    public List<Guid> SelectedEndpointIds { get; set; } = new();

    /// <summary>
    /// Optional business rules per endpoint. Key = EndpointId, Value = plain text business rule.
    /// </summary>
    public Dictionary<Guid, string> EndpointBusinessContexts { get; set; } = new();

    /// <summary>
    /// Optional global business rules (free text) that apply to all endpoints in this suite.
    /// AI Agent uses this as high-level context for test generation.
    /// </summary>
    [MaxLength(8000)]
    public string GlobalBusinessRules { get; set; }
}
