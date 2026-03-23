namespace ClassifiedAds.Modules.TestReporting.Models.Requests;

public class GenerateTestReportRequest
{
    public string ReportType { get; set; }

    public string Format { get; set; }

    public int? RecentHistoryLimit { get; set; }
}
