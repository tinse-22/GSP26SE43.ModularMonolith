using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class ResolvedTestCaseRequest
{
    public Guid TestCaseId { get; set; }

    public string Name { get; set; }

    public string HttpMethod { get; set; }

    public string ResolvedUrl { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();

    public Dictionary<string, string> QueryParams { get; set; } = new();

    public string Body { get; set; }

    public string BodyType { get; set; }

    public int TimeoutMs { get; set; }

    public IReadOnlyList<Guid> DependencyIds { get; set; } = Array.Empty<Guid>();

    /// <summary>The per-execution unique ID injected as {{tcUniqueId}} during resolution.</summary>
    public string TcUniqueId { get; set; }
}
