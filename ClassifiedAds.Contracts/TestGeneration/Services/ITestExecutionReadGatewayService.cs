using ClassifiedAds.Contracts.TestGeneration.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.TestGeneration.Services;

public interface ITestExecutionReadGatewayService
{
    Task<TestSuiteAccessContextDto> GetSuiteAccessContextAsync(
        Guid testSuiteId,
        CancellationToken ct = default);

    Task<TestSuiteExecutionContextDto> GetExecutionContextAsync(
        Guid testSuiteId,
        IReadOnlyCollection<Guid> selectedTestCaseIds,
        CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetTestCaseIdsBySuiteAsync(
        Guid testSuiteId,
        CancellationToken ct = default);
}
