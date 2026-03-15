using ClassifiedAds.Contracts.TestExecution.DTOs;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.TestExecution.Services;

public interface ITestFailureReadGatewayService
{
    Task<TestFailureExplanationContextDto> GetFailureExplanationContextAsync(
        Guid testSuiteId,
        Guid runId,
        Guid testCaseId,
        CancellationToken ct = default);
}
