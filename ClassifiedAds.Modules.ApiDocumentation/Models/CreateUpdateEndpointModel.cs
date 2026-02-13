using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class CreateUpdateEndpointModel
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
