using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.TestGeneration.Services;

/// <summary>
/// Cross-module contract for project-level test suite operations.
/// Implemented by ClassifiedAds.Modules.TestGeneration.
/// </summary>
public interface ITestSuiteProjectService
{
    /// <summary>
    /// Archives all non-archived test suites belonging to the given project.
    /// Called when a project is soft-deleted so its suites are no longer listed.
    /// </summary>
    Task ArchiveByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default);
}
