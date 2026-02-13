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

public class GetEndpointsQuery : IQuery<List<EndpointModel>>
{
    public Guid ProjectId { get; set; }

    public Guid SpecId { get; set; }

    public Guid OwnerId { get; set; }
}

public class GetEndpointsQueryHandler : IQueryHandler<GetEndpointsQuery, List<EndpointModel>>
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;

    public GetEndpointsQueryHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
    }

    public async Task<List<EndpointModel>> HandleAsync(GetEndpointsQuery query, CancellationToken cancellationToken = default)
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

        // Verify spec belongs to project
        var spec = await _specRepository.FirstOrDefaultAsync(
            _specRepository.GetQueryableSet().Where(s => s.Id == query.SpecId && s.ProjectId == query.ProjectId));

        if (spec == null)
        {
            throw new NotFoundException($"Không tìm thấy specification với mã '{query.SpecId}'.");
        }

        // Load endpoints
        var endpoints = await _endpointRepository.GetQueryableSet()
            .Where(e => e.ApiSpecId == query.SpecId)
            .OrderByDescending(e => e.CreatedDateTime)
            .ToListAsync(cancellationToken);

        return endpoints.Select(e => new EndpointModel
        {
            Id = e.Id,
            ApiSpecId = e.ApiSpecId,
            HttpMethod = e.HttpMethod.ToString(),
            Path = e.Path,
            OperationId = e.OperationId,
            Summary = e.Summary,
            Description = e.Description,
            Tags = e.Tags,
            IsDeprecated = e.IsDeprecated,
            CreatedDateTime = e.CreatedDateTime,
            UpdatedDateTime = e.UpdatedDateTime,
        }).ToList();
    }
}
