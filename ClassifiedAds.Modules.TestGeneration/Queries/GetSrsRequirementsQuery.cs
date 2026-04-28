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

public class GetSrsRequirementsQuery : IQuery<List<SrsRequirementModel>>
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid CurrentUserId { get; set; }

    /// <summary>Optional filter by RequirementType.</summary>
    public SrsRequirementType? RequirementType { get; set; }

    /// <summary>Optional filter by IsReviewed status.</summary>
    public bool? IsReviewed { get; set; }

    /// <summary>Optional filter by EndpointId.</summary>
    public Guid? EndpointId { get; set; }
}

public class GetSrsRequirementsQueryHandler : IQueryHandler<GetSrsRequirementsQuery, List<SrsRequirementModel>>
{
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;

    public GetSrsRequirementsQueryHandler(
        IRepository<SrsRequirement, Guid> requirementRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository)
    {
        _requirementRepository = requirementRepository;
        _srsDocumentRepository = srsDocumentRepository;
    }

    public async Task<List<SrsRequirementModel>> HandleAsync(GetSrsRequirementsQuery query, CancellationToken cancellationToken = default)
    {
        // Validate document ownership
        var docExists = await _srsDocumentRepository.GetQueryableSet()
            .AnyAsync(x => x.Id == query.SrsDocumentId && x.ProjectId == query.ProjectId && !x.IsDeleted, cancellationToken);

        if (!docExists)
        {
            throw new NotFoundException($"SrsDocument {query.SrsDocumentId} khong tim thay.");
        }

        var reqs = _requirementRepository.GetQueryableSet()
            .Where(x => x.SrsDocumentId == query.SrsDocumentId);

        if (query.RequirementType.HasValue)
        {
            reqs = reqs.Where(x => x.RequirementType == query.RequirementType.Value);
        }

        if (query.IsReviewed.HasValue)
        {
            reqs = reqs.Where(x => x.IsReviewed == query.IsReviewed.Value);
        }

        if (query.EndpointId.HasValue)
        {
            reqs = reqs.Where(x => x.EndpointId == query.EndpointId.Value);
        }

        var list = await _requirementRepository.ToListAsync(
            reqs.OrderBy(x => x.DisplayOrder));

        return list.Select(SrsRequirementModel.FromEntity).ToList();
    }
}

public class GetSrsRequirementDetailQuery : IQuery<SrsRequirementModel>
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid RequirementId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetSrsRequirementDetailQueryHandler : IQueryHandler<GetSrsRequirementDetailQuery, SrsRequirementModel>
{
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;

    public GetSrsRequirementDetailQueryHandler(
        IRepository<SrsRequirement, Guid> requirementRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository)
    {
        _requirementRepository = requirementRepository;
        _srsDocumentRepository = srsDocumentRepository;
    }

    public async Task<SrsRequirementModel> HandleAsync(GetSrsRequirementDetailQuery query, CancellationToken cancellationToken = default)
    {
        var docExists = await _srsDocumentRepository.GetQueryableSet()
            .AnyAsync(x => x.Id == query.SrsDocumentId && x.ProjectId == query.ProjectId && !x.IsDeleted, cancellationToken);

        if (!docExists)
        {
            throw new NotFoundException($"SrsDocument {query.SrsDocumentId} khong tim thay.");
        }

        var req = await _requirementRepository.FirstOrDefaultAsync(
            _requirementRepository.GetQueryableSet()
                .Where(x => x.Id == query.RequirementId && x.SrsDocumentId == query.SrsDocumentId));

        if (req == null)
        {
            throw new NotFoundException($"SrsRequirement {query.RequirementId} khong tim thay.");
        }

        return SrsRequirementModel.FromEntity(req);
    }
}

public class GetSrsRequirementClarificationsQuery : IQuery<List<SrsRequirementClarificationModel>>
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid RequirementId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetSrsRequirementClarificationsQueryHandler
    : IQueryHandler<GetSrsRequirementClarificationsQuery, List<SrsRequirementClarificationModel>>
{
    private readonly IRepository<SrsRequirementClarification, Guid> _clarificationRepository;
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;

    public GetSrsRequirementClarificationsQueryHandler(
        IRepository<SrsRequirementClarification, Guid> clarificationRepository,
        IRepository<SrsRequirement, Guid> requirementRepository)
    {
        _clarificationRepository = clarificationRepository;
        _requirementRepository = requirementRepository;
    }

    public async Task<List<SrsRequirementClarificationModel>> HandleAsync(
        GetSrsRequirementClarificationsQuery query,
        CancellationToken cancellationToken = default)
    {
        var reqExists = await _requirementRepository.GetQueryableSet()
            .AnyAsync(x => x.Id == query.RequirementId && x.SrsDocumentId == query.SrsDocumentId, cancellationToken);

        if (!reqExists)
        {
            throw new NotFoundException($"SrsRequirement {query.RequirementId} khong tim thay.");
        }

        var clars = await _clarificationRepository.ToListAsync(
            _clarificationRepository.GetQueryableSet()
                .Where(x => x.SrsRequirementId == query.RequirementId)
                .OrderBy(x => x.DisplayOrder));

        return clars.Select(SrsRequirementClarificationModel.FromEntity).ToList();
    }
}
