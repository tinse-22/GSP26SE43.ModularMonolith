using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Queries;

public class GetProjectQuery : IQuery<ProjectDetailModel>
{
    public Guid ProjectId { get; set; }

    public Guid OwnerId { get; set; }
}

public class GetProjectQueryHandler : IQueryHandler<GetProjectQuery, ProjectDetailModel>
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;

    public GetProjectQueryHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
    }

    public async Task<ProjectDetailModel> HandleAsync(GetProjectQuery query, CancellationToken cancellationToken = default)
    {
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

        var specCount = await _specRepository.GetQueryableSet()
            .Where(s => s.ProjectId == project.Id)
            .CountAsync(cancellationToken);

        SpecSummaryModel activeSpecSummary = null;
        if (project.ActiveSpecId.HasValue)
        {
            var activeSpec = await _specRepository.FirstOrDefaultAsync(
                _specRepository.GetQueryableSet().Where(s => s.Id == project.ActiveSpecId.Value));

            if (activeSpec != null)
            {
                var endpointCount = await _endpointRepository.GetQueryableSet()
                    .Where(e => e.ApiSpecId == activeSpec.Id)
                    .CountAsync(cancellationToken);

                activeSpecSummary = new SpecSummaryModel
                {
                    Id = activeSpec.Id,
                    Name = activeSpec.Name,
                    SourceType = activeSpec.SourceType.ToString(),
                    Version = activeSpec.Version,
                    ParseStatus = activeSpec.ParseStatus.ToString(),
                    EndpointCount = endpointCount,
                };
            }
        }

        return new ProjectDetailModel
        {
            Id = project.Id,
            Name = project.Name,
            Description = project.Description,
            BaseUrl = project.BaseUrl,
            Status = project.Status.ToString(),
            ActiveSpecId = project.ActiveSpecId,
            ActiveSpecName = activeSpecSummary?.Name,
            CreatedDateTime = project.CreatedDateTime,
            UpdatedDateTime = project.UpdatedDateTime,
            TotalSpecifications = specCount,
            ActiveSpecSummary = activeSpecSummary,
        };
    }
}
