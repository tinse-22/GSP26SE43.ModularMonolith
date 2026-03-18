namespace ClassifiedAds.Modules.TestReporting.ConfigurationOptions;

public class ReportGenerationOptions
{
    public int DefaultHistoryLimit { get; set; } = 5;

    public int MaxHistoryLimit { get; set; } = 20;

    public int MaxResponseBodyPreviewChars { get; set; } = 4000;

    public int ReportRetentionHours { get; set; } = 168;
}
