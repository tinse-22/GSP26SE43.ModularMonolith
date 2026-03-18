using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Services;

/// <summary>
/// Interface for parsing API specification files into normalized entities.
/// </summary>
public interface ISpecificationParser
{
    /// <summary>
    /// Returns true if this parser handles the given source type.
    /// </summary>
    bool CanParse(SourceType sourceType);

    /// <summary>
    /// Parses specification file content into normalized endpoint/security models.
    /// </summary>
    Task<SpecificationParseResult> ParseAsync(byte[] content, string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of parsing a specification file.
/// </summary>
public class SpecificationParseResult
{
    public bool Success { get; set; }

    public List<string> Errors { get; set; } = new();

    public string DetectedVersion { get; set; }

    public List<ParsedEndpoint> Endpoints { get; set; } = new();

    public List<ParsedSecurityScheme> SecuritySchemes { get; set; } = new();
}

/// <summary>
/// Parsed endpoint definition from a specification file.
/// </summary>
public class ParsedEndpoint
{
    public string HttpMethod { get; set; }

    public string Path { get; set; }

    public string OperationId { get; set; }

    public string Summary { get; set; }

    public string Description { get; set; }

    public List<string> Tags { get; set; } = new();

    public bool IsDeprecated { get; set; }

    public List<ParsedParameter> Parameters { get; set; } = new();

    public List<ParsedResponse> Responses { get; set; } = new();

    public List<ParsedSecurityRequirement> SecurityRequirements { get; set; } = new();
}

/// <summary>
/// Parsed parameter definition from a specification file.
/// </summary>
public class ParsedParameter
{
    public string Name { get; set; }

    /// <summary>
    /// Parameter location: Path, Query, Header, Body, Cookie.
    /// </summary>
    public string Location { get; set; }

    public string DataType { get; set; }

    public string Format { get; set; }

    public bool IsRequired { get; set; }

    public string DefaultValue { get; set; }

    /// <summary>
    /// JSON Schema as serialized JSON string.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Example values as serialized JSON string.
    /// </summary>
    public string Examples { get; set; }
}

/// <summary>
/// Parsed response definition from a specification file.
/// </summary>
public class ParsedResponse
{
    public int StatusCode { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// Response JSON Schema as serialized JSON string.
    /// </summary>
    public string Schema { get; set; }

    /// <summary>
    /// Example responses as serialized JSON string.
    /// </summary>
    public string Examples { get; set; }

    /// <summary>
    /// Response headers as serialized JSON string.
    /// </summary>
    public string Headers { get; set; }
}

/// <summary>
/// Parsed security scheme definition from a specification file.
/// </summary>
public class ParsedSecurityScheme
{
    public string Name { get; set; }

    public SchemeType SchemeType { get; set; }

    public string Scheme { get; set; }

    public string BearerFormat { get; set; }

    public ApiKeyLocation? ApiKeyLocation { get; set; }

    public string ParameterName { get; set; }

    /// <summary>
    /// Additional configuration (OAuth2 flows, etc.) as serialized JSON string.
    /// </summary>
    public string Configuration { get; set; }
}

/// <summary>
/// Parsed security requirement for an endpoint.
/// </summary>
public class ParsedSecurityRequirement
{
    public SecurityType SecurityType { get; set; }

    public string SchemeName { get; set; }

    /// <summary>
    /// Required scopes as serialized JSON array string.
    /// </summary>
    public string Scopes { get; set; }
}
