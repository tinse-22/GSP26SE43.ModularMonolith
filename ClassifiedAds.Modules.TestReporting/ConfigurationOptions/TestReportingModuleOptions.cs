namespace ClassifiedAds.Modules.TestReporting.ConfigurationOptions;

public class TestReportingModuleOptions
{
    public ConnectionStringsOptions ConnectionStrings { get; set; } = new ConnectionStringsOptions();

    public ReportGenerationOptions ReportGeneration { get; set; } = new ReportGenerationOptions();
}
