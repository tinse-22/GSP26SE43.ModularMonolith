using ClassifiedAds.Contracts.TestExecution.JsonPathResolution;

namespace ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;

public class TestGenerationModuleOptions
{
    public ConnectionStringsOptions ConnectionStrings { get; set; }

    public N8nIntegrationOptions N8nIntegration { get; set; }

    public ScenarioGenerationBudgetOptions ScenarioGenerationBudget { get; set; } = new();

    public JsonPathResolutionOptions JsonPathResolution { get; set; } = new();
}
