using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetGenerationJobsQuery : IQuery<List<GenerationJobStatusDto>>
{
    public Guid TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
    public int Limit { get; set; } = 20;
}

public class GetGenerationJobsQueryHandler : IQueryHandler<GetGenerationJobsQuery, List<GenerationJobStatusDto>>
{
    private readonly IRepository<TestGenerationJob, Guid> _jobRepository;
    private readonly IRepository<TestSuite, Guid> _suiteRepository;

    public GetGenerationJobsQueryHandler(
        IRepository<TestGenerationJob, Guid> jobRepository,
        IRepository<TestSuite, Guid> suiteRepository)
    {
        _jobRepository = jobRepository;
        _suiteRepository = suiteRepository;
    }

    public async Task<List<GenerationJobStatusDto>> HandleAsync(
        GetGenerationJobsQuery query,
        CancellationToken cancellationToken = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == query.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Test suite '{query.TestSuiteId}' was not found.");
        }

        if (suite.CreatedById != query.CurrentUserId)
        {
            throw new ValidationException("You do not have permission to view generation jobs for this suite.");
        }

        var limit = query.Limit <= 0 ? 20 : Math.Min(query.Limit, 100);
        var jobs = await _jobRepository.ToListAsync(
            _jobRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == query.TestSuiteId)
                .OrderByDescending(x => x.QueuedAt)
                .Take(limit));

        return jobs
            .Select(job => new GenerationJobStatusDto
            {
                JobId = job.Id,
                TestSuiteId = job.TestSuiteId,
                Status = job.Status.ToString(),
                QueuedAt = job.QueuedAt,
                TriggeredAt = job.TriggeredAt,
                CompletedAt = job.CompletedAt,
                TestCasesGenerated = job.TestCasesGenerated,
                ErrorMessage = job.ErrorMessage,
                WebhookName = job.WebhookName,
            })
            .ToList();
    }
}

