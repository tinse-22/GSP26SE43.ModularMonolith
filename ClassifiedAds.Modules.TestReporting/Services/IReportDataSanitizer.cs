using ClassifiedAds.Contracts.TestExecution.DTOs;

namespace ClassifiedAds.Modules.TestReporting.Services;

public interface IReportDataSanitizer
{
    TestRunReportContextDto Sanitize(TestRunReportContextDto context);
}
