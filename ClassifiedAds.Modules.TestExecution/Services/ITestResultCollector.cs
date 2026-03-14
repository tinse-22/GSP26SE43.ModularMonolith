using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Services;

public interface ITestResultCollector
{
    Task<TestRunResultModel> CollectAsync(
        TestRun run,
        IReadOnlyList<TestCaseExecutionResult> caseResults,
        int retentionDays,
        string environmentName,
        CancellationToken ct = default);
}
