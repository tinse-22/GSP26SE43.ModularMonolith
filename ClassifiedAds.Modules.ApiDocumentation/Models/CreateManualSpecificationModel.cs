using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class CreateManualSpecificationModel
{
    public string Name { get; set; }

    public string Version { get; set; }

    public bool AutoActivate { get; set; }

    public List<ManualEndpointDefinition> Endpoints { get; set; } = new();
}

public class ManualEndpointDefinition
{
    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public string OperationId { get; set; }

    public string Summary { get; set; }

    public string Description { get; set; }

    public List<string> Tags { get; set; }

    public bool IsDeprecated { get; set; }

    public List<ManualParameterDefinition> Parameters { get; set; } = new();

    public List<ManualResponseDefinition> Responses { get; set; } = new();
}

public class ManualParameterDefinition
{
    public string Name { get; set; }

    public string Location { get; set; }

    public EndpointParameterDataType DataType { get; set; } = EndpointParameterDataType.String;

    public string Format { get; set; }

    public bool IsRequired { get; set; }

    public string DefaultValue { get; set; }

    public string Schema { get; set; }

    public string Examples { get; set; }
}

public class ManualResponseDefinition
{
    public int StatusCode { get; set; }

    public string Description { get; set; }

    public string Schema { get; set; }

    public string Examples { get; set; }

    public string Headers { get; set; }
}
