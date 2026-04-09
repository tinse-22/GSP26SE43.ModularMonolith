using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetGenerationJobStatusQuery : IQuery<GenerationJobStatusDto>
{
    public Guid JobId { get; set; }
    public Guid? TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
}

public class GenerationJobStatusDto
{
    public Guid JobId { get; set; }
    public Guid TestSuiteId { get; set; }
    public string Status { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? TriggeredAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int? TestCasesGenerated { get; set; }
    public string ErrorMessage { get; set; }
    public string WebhookName { get; set; }
}

public class GetGenerationJobStatusQueryHandler : IQueryHandler<GetGenerationJobStatusQuery, GenerationJobStatusDto>
{
    private readonly IRepository<TestGenerationJob, Guid> _jobRepository;
    private readonly IRepository<TestSuite, Guid> _suiteRepository;

    public GetGenerationJobStatusQueryHandler(
        IRepository<TestGenerationJob, Guid> jobRepository,
        IRepository<TestSuite, Guid> suiteRepository)
    {
        _jobRepository = jobRepository;
        _suiteRepository = suiteRepository;
    }

    public async Task<GenerationJobStatusDto> HandleAsync(
        GetGenerationJobStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        TestGenerationJob job;

        if (query.JobId != Guid.Empty)
        {
            // Get by job ID
            job = await _jobRepository.FirstOrDefaultAsync(
                _jobRepository.GetQueryableSet().Where(x => x.Id == query.JobId));
        }
        else if (query.TestSuiteId.HasValue && query.TestSuiteId.Value != Guid.Empty)
        {
            // Get latest job for suite
            job = await _jobRepository.FirstOrDefaultAsync(
                _jobRepository.GetQueryableSet()
                    .Where(x => x.TestSuiteId == query.TestSuiteId.Value)
                    .OrderByDescending(x => x.QueuedAt));
        }
        else
        {
            throw new ValidationException("JobId hoặc TestSuiteId là bắt buộc.");
        }

        if (job == null)
        {
            throw new NotFoundException("Không tìm thấy generation job.");
        }

        // Verify ownership
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == job.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite '{job.TestSuiteId}'.");
        }

        if (suite.CreatedById != query.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền xem generation job này.");
        }

        return new GenerationJobStatusDto
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
        };
    }
}
