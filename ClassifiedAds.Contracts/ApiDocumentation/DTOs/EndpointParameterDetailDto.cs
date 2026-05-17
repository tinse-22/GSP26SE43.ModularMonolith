using System;
using System.Collections.Generic;

namespace ClassifiedAds.Contracts.ApiDocumentation.DTOs;

/// <summary>
/// Parameter details for an API endpoint, grouped by endpoint.
/// </summary>
public class EndpointParameterDetailDto
{
    public Guid EndpointId { get; set; }

    public string EndpointPath { get; set; }

    public string EndpointHttpMethod { get; set; }

    public IReadOnlyList<ParameterDetailDto> Parameters { get; set; } = Array.Empty<ParameterDetailDto>();
}

/// <summary>
/// Structured parameter detail for mutation generation.
/// </summary>
public class ParameterDetailDto
{
    public Guid ParameterId { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// Parameter location: Path, Query, Header, Body, Cookie.
    /// </summary>
    public string Location { get; set; }

    /// <summary>
    /// Data type: string, integer, number, boolean, array, object.
    /// </summary>
    public string DataType { get; set; }

    /// <summary>
    /// Format: int32, int64, float, double, uuid, email, date-time, etc.
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Request body media type when Location is Body.
    /// </summary>
    public string ContentType { get; set; }

    public bool IsRequired { get; set; }

    public string DefaultValue { get; set; }

    /// <summary>
    /// Raw JSON Schema for validation constraints.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Example values as JSON.
    /// </summary>
    public string Examples { get; set; }
}
