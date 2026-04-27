using ClassifiedAds.Contracts.TestExecution.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.TestExecution.Services;

/// <summary>
/// Cross-module read gateway: TestGeneration queries TestExecution for execution evidence
/// without breaking module boundaries (TestGeneration never touches TestExecution DbContext directly).
/// </summary>
public interface ITestCaseExecutionEvidenceReadGatewayService
{
    /// <summary>
    /// Returns the latest execution evidence per test case for the given suite.
    /// If <paramref name="testRunId"/> is provided, evidence comes from that specific run.
    /// Otherwise, the latest finished run (Completed or Failed lifecycle status) is used.
    /// Returns an empty list when no finished run exists for the suite.
    /// </summary>
    Task<IReadOnlyList<TestCaseExecutionEvidenceDto>> GetLatestEvidenceByTestSuiteAsync(
        Guid testSuiteId,
        Guid? testRunId,
        CancellationToken cancellationToken = default);
}
