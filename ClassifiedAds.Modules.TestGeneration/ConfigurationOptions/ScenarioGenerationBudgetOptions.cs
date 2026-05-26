namespace ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;

public class ScenarioGenerationBudgetOptions
{
    public int SimpleEndpointSoftLimit { get; set; } = 3;

    public int ComplexEndpointSoftLimit { get; set; } = 10;

    public int DefaultHardLimitPerEndpoint { get; set; } = 15;

    public int MaxScenarioBudgetPerBatch { get; set; } = 20;
}
