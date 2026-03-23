using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class TestRunResultModel
{
    public TestRunModel Run { get; set; }

    public string ResultsSource { get; set; } = "cache";

    public DateTimeOffset ExecutedAt { get; set; }

    public string ResolvedEnvironmentName { get; set; }

    public List<TestCaseRunResultModel> Cases { get; set; } = new();
}
