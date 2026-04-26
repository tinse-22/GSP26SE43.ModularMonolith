using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Queries;

public class GetSrsDocumentsQuery : IQuery<List<SrsDocumentModel>>
{
    public Guid ProjectId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetSrsDocumentsQueryHandler : IQueryHandler<GetSrsDocumentsQuery, List<SrsDocumentModel>>
{
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;

    public GetSrsDocumentsQueryHandler(IRepository<SrsDocument, Guid> srsDocumentRepository)
    {
        _srsDocumentRepository = srsDocumentRepository;
    }

    public async Task<List<SrsDocumentModel>> HandleAsync(GetSrsDocumentsQuery query, CancellationToken cancellationToken = default)
    {
        var docs = await _srsDocumentRepository.ToListAsync(
            _srsDocumentRepository.GetQueryableSet()
                .Where(x => x.ProjectId == query.ProjectId
                    && x.CreatedById == query.CurrentUserId
                    && !x.IsDeleted)
                .OrderByDescending(x => x.CreatedDateTime));

        return docs.Select(d => SrsDocumentModel.FromEntity(d)).ToList();
    }
}

public class GetSrsDocumentDetailQuery : IQuery<SrsDocumentModel>
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetSrsDocumentDetailQueryHandler : IQueryHandler<GetSrsDocumentDetailQuery, SrsDocumentModel>
{
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;

    public GetSrsDocumentDetailQueryHandler(
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<SrsRequirement, Guid> requirementRepository)
    {
        _srsDocumentRepository = srsDocumentRepository;
        _requirementRepository = requirementRepository;
    }

    public async Task<SrsDocumentModel> HandleAsync(GetSrsDocumentDetailQuery query, CancellationToken cancellationToken = default)
    {
        var doc = await _srsDocumentRepository.FirstOrDefaultAsync(
            _srsDocumentRepository.GetQueryableSet()
                .Where(x => x.Id == query.SrsDocumentId
                    && x.ProjectId == query.ProjectId
                    && !x.IsDeleted));

        if (doc == null)
        {
            throw new NotFoundException($"SrsDocument {query.SrsDocumentId} khong tim thay.");
        }

        var requirements = await _requirementRepository.ToListAsync(
            _requirementRepository.GetQueryableSet()
                .Where(x => x.SrsDocumentId == query.SrsDocumentId)
                .OrderBy(x => x.DisplayOrder));

        return SrsDocumentModel.FromEntity(doc, requirements.Select(SrsRequirementModel.FromEntity));
    }
}
