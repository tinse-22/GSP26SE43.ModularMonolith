using ClassifiedAds.Contracts.TestExecution.JsonPathResolution;

namespace ClassifiedAds.Modules.TestExecution.ConfigurationOptions;

public class TestExecutionModuleOptions
{
    public ConnectionStringsOptions ConnectionStrings { get; set; }

    public JsonPathResolutionOptions JsonPathResolution { get; set; } = new();
}
