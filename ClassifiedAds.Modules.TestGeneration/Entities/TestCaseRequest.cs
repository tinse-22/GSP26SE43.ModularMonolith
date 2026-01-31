using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Request definition for a test case (1:1 relationship).
/// </summary>
public class TestCaseRequest : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Test case this request belongs to.
    /// </summary>
    public Guid TestCaseId { get; set; }

    /// <summary>
    /// HTTP method: GET, POST, PUT, DELETE, PATCH.
    /// </summary>
    public HttpMethod HttpMethod { get; set; }

    /// <summary>
    /// Full URL or path template.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Request headers as JSON.
    /// </summary>
    public string Headers { get; set; }

    /// <summary>
    /// Path parameters as JSON.
    /// </summary>
    public string PathParams { get; set; }

    /// <summary>
    /// Query parameters as JSON.
    /// </summary>
    public string QueryParams { get; set; }

    /// <summary>
    /// Body type: JSON, FormData, UrlEncoded, Raw.
    /// </summary>
    public BodyType BodyType { get; set; }

    /// <summary>
    /// Request body content.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Request timeout in milliseconds.
    /// </summary>
    public int Timeout { get; set; } = 30000;

    // Navigation properties
    public TestCase TestCase { get; set; }
}

public enum HttpMethod
{
    GET = 0,
    POST = 1,
    PUT = 2,
    DELETE = 3,
    PATCH = 4,
    HEAD = 5,
    OPTIONS = 6
}

public enum BodyType
{
    None = 0,
    JSON = 1,
    FormData = 2,
    UrlEncoded = 3,
    Raw = 4,
    Binary = 5
}
