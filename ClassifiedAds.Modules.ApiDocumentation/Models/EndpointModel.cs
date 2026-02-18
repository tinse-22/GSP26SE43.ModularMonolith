using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

public class EndpointModel
{
    public Guid Id { get; set; }

    public Guid ApiSpecId { get; set; }

    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public string OperationId { get; set; }

    public string Summary { get; set; }

    public string Description { get; set; }

    public string Tags { get; set; }

    public bool IsDeprecated { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }
}

public class EndpointDetailModel : EndpointModel
{
    public string ResolvedUrl { get; set; }

    public List<ParameterModel> Parameters { get; set; } = new();

    public List<ResponseModel> Responses { get; set; } = new();

    public List<SecurityReqModel> SecurityRequirements { get; set; } = new();
}

public class ParameterModel
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Location { get; set; }

    public string DataType { get; set; }

    public string Format { get; set; }

    public bool IsRequired { get; set; }

    public string DefaultValue { get; set; }

    public string Schema { get; set; }

    public string Examples { get; set; }
}

public class ResponseModel
{
    public Guid Id { get; set; }

    public int StatusCode { get; set; }

    public string Description { get; set; }

    public string Schema { get; set; }

    public string Examples { get; set; }

    public string Headers { get; set; }
}

public class SecurityReqModel
{
    public Guid Id { get; set; }

    public string SecurityType { get; set; }

    public string SchemeName { get; set; }

    public string Scopes { get; set; }
}
