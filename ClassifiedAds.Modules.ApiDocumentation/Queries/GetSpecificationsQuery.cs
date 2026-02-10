using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Queries;

public class GetSpecificationsQuery : IQuery<List<SpecificationModel>>
{
    public Guid ProjectId { get; set; }

    public Guid OwnerId { get; set; }

    public ParseStatus? ParseStatus { get; set; }

    public SourceType? SourceType { get; set; }
}

public class GetSpecificationsQueryHandler : IQueryHandler<GetSpecificationsQuery, List<SpecificationModel>>
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;

    public GetSpecificationsQueryHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
    }

    public async Task<List<SpecificationModel>> HandleAsync(GetSpecificationsQuery query, CancellationToken cancellationToken = default)
    {
        // Verify project exists and ownership
        var project = await _projectRepository.FirstOrDefaultAsync(
            _projectRepository.GetQueryableSet().Where(p => p.Id == query.ProjectId));

        if (project == null)
        {
            throw new NotFoundException($"Không tìm thấy project với mã '{query.ProjectId}'.");
        }

        if (project.OwnerId != query.OwnerId)
        {
            throw new NotFoundException($"Không tìm thấy project với mã '{query.ProjectId}'.");
        }

        var specsQuery = _specRepository.GetQueryableSet()
            .Where(s => s.ProjectId == query.ProjectId);

        if (query.ParseStatus.HasValue)
        {
            specsQuery = specsQuery.Where(s => s.ParseStatus == query.ParseStatus.Value);
        }

        if (query.SourceType.HasValue)
        {
            specsQuery = specsQuery.Where(s => s.SourceType == query.SourceType.Value);
        }

        var specs = await specsQuery
            .OrderByDescending(s => s.CreatedDateTime)
            .ToListAsync(cancellationToken);

        return specs.Select(s => new SpecificationModel
        {
            Id = s.Id,
            ProjectId = s.ProjectId,
            Name = s.Name,
            SourceType = s.SourceType.ToString(),
            Version = s.Version,
            IsActive = s.IsActive,
            ParseStatus = s.ParseStatus.ToString(),
            ParsedAt = s.ParsedAt,
            OriginalFileId = s.OriginalFileId,
            CreatedDateTime = s.CreatedDateTime,
            UpdatedDateTime = s.UpdatedDateTime,
        }).ToList();
    }
}
