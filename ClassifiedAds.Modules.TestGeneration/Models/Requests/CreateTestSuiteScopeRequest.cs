using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

public class CreateTestSuiteScopeRequest
{
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
    /// Example: { "ep-id": "Only allow registration when user >= 17 years old" }
    /// </summary>
    public Dictionary<Guid, string> EndpointBusinessContexts { get; set; } = new();

    /// <summary>
    /// Optional global business rules (free text) that apply to all endpoints in this suite.
    /// AI Agent uses this as high-level context for test generation.
    /// Example: "Users must verify email before placing orders. All monetary amounts use VND currency."
    /// </summary>
    [MaxLength(8000)]
    public string GlobalBusinessRules { get; set; }
}
