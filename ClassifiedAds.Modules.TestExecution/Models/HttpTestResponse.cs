using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class HttpTestResponse
{
    public int? StatusCode { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();

    public string Body { get; set; }

    public long LatencyMs { get; set; }

    public string TransportError { get; set; }

    public string ContentType { get; set; }
}
