using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetSrsAnalysisJobQuery : IQuery<SrsAnalysisJobModel>
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid JobId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetSrsAnalysisJobQueryHandler : IQueryHandler<GetSrsAnalysisJobQuery, SrsAnalysisJobModel>
{
    private readonly IRepository<SrsAnalysisJob, Guid> _jobRepository;

    public GetSrsAnalysisJobQueryHandler(IRepository<SrsAnalysisJob, Guid> jobRepository)
    {
        _jobRepository = jobRepository;
    }

    public async Task<SrsAnalysisJobModel> HandleAsync(GetSrsAnalysisJobQuery query, CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.FirstOrDefaultAsync(
            _jobRepository.GetQueryableSet()
                .Where(x => x.Id == query.JobId && x.SrsDocumentId == query.SrsDocumentId));

        if (job == null)
        {
            throw new NotFoundException($"SrsAnalysisJob {query.JobId} khong tim thay.");
        }

        return SrsAnalysisJobModel.FromEntity(job);
    }
}
