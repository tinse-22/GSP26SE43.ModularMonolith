using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

public class ApiOrderItemModel
{
    public Guid EndpointId { get; set; }

    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public int OrderIndex { get; set; }

    public List<Guid> DependsOnEndpointIds { get; set; } = new List<Guid>();

    public List<string> ReasonCodes { get; set; } = new List<string>();

    public bool IsAuthRelated { get; set; }
}
