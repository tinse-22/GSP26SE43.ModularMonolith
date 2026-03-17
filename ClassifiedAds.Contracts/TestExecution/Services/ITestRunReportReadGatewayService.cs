using ClassifiedAds.Contracts.TestExecution.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.TestExecution.Services;

public interface ITestRunReportReadGatewayService
{
    Task<TestRunReportContextDto> GetReportContextAsync(
        Guid testSuiteId,
        Guid runId,
        int recentHistoryLimit = 5,
        CancellationToken ct = default);
}
