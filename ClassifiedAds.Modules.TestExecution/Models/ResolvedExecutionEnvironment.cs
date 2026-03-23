using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class ResolvedExecutionEnvironment
{
    public Guid EnvironmentId { get; set; }

    public string Name { get; set; }

    public string BaseUrl { get; set; }

    public Dictionary<string, string> Variables { get; set; } = new();

    public Dictionary<string, string> DefaultHeaders { get; set; } = new();

    public Dictionary<string, string> DefaultQueryParams { get; set; } = new();
}
