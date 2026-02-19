using System;
using System.Collections.Generic;

namespace ClassifiedAds.Contracts.ApiDocumentation.DTOs;

public class ApiEndpointMetadataDto
{
    public Guid EndpointId { get; set; }

    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public string OperationId { get; set; }

    public bool IsAuthRelated { get; set; }

    public IReadOnlyCollection<Guid> DependsOnEndpointIds { get; set; } = Array.Empty<Guid>();
}
