namespace ClassifiedAds.Modules.LlmAssistant.ConfigurationOptions;

public class LlmAssistantModuleOptions
{
    public ConnectionStringsOptions ConnectionStrings { get; set; }

    public FailureExplanationOptions FailureExplanation { get; set; } = new FailureExplanationOptions();
}
