using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Services;

public interface ITestExecutionOrchestrator
{
    Task<TestRunResultModel> ExecuteAsync(
        Guid testRunId,
        Guid testSuiteId,
        Guid environmentId,
        Guid currentUserId,
        IReadOnlyCollection<Guid> selectedTestCaseIds,
        CancellationToken ct = default);
}
