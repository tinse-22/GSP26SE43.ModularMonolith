using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Payload sent to n8n webhook for boundary/negative test scenario generation.
/// </summary>
public class N8nBoundaryNegativePayload
{
    public Guid TestSuiteId { get; set; }

    public string TestSuiteName { get; set; }

    public string GlobalBusinessRules { get; set; }

    public List<N8nBoundaryEndpointPayload> Endpoints { get; set; } = new();
}

public class N8nBoundaryEndpointPayload
{
    public Guid EndpointId { get; set; }

    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public string OperationId { get; set; }

    public int OrderIndex { get; set; }

    public string BusinessContext { get; set; }

    public N8nPromptPayload Prompt { get; set; }

    public List<string> ParameterSchemaPayloads { get; set; } = new();

    public List<string> ResponseSchemaPayloads { get; set; } = new();

    public List<N8nParameterDetail> ParameterDetails { get; set; } = new();
}

public class N8nParameterDetail
{
    public string Name { get; set; }

    public string Location { get; set; }

    public string DataType { get; set; }

    public string Format { get; set; }

    public bool IsRequired { get; set; }

    public string DefaultValue { get; set; }
}
