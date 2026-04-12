using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public class TestSuiteProjectService : ITestSuiteProjectService
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly ILogger<TestSuiteProjectService> _logger;

    public TestSuiteProjectService(
        IRepository<TestSuite, Guid> suiteRepository,
        ILogger<TestSuiteProjectService> logger)
    {
        _suiteRepository = suiteRepository;
        _logger = logger;
    }

    public async Task ArchiveByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var suites = await _suiteRepository.ToListAsync(
            _suiteRepository.GetQueryableSet()
                .Where(s => s.ProjectId == projectId && s.Status != TestSuiteStatus.Archived));

        if (suites.Count == 0)
        {
            return;
        }

        foreach (var suite in suites)
        {
            suite.Status = TestSuiteStatus.Archived;
            await _suiteRepository.UpdateAsync(suite, cancellationToken);
        }

        await _suiteRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cascade-archived {Count} test suite(s) for deleted project. ProjectId={ProjectId}",
            suites.Count,
            projectId);
    }
}
