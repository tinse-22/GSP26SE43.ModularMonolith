using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Queries;

public class GetProjectsQuery : IQuery<PaginatedResult<ProjectModel>>
{
    public Guid OwnerId { get; set; }

    public ProjectStatus? Status { get; set; }

    public string Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public class GetProjectsQueryHandler : IQueryHandler<GetProjectsQuery, PaginatedResult<ProjectModel>>
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;

    public GetProjectsQueryHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
    }

    public async Task<PaginatedResult<ProjectModel>> HandleAsync(GetProjectsQuery query, CancellationToken cancellationToken = default)
    {
        var projectsQuery = _projectRepository.GetQueryableSet()
            .Where(p => p.OwnerId == query.OwnerId);

        if (query.Status.HasValue)
        {
            projectsQuery = projectsQuery.Where(p => p.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToLower();
            projectsQuery = projectsQuery.Where(p => p.Name.ToLower().Contains(search));
        }

        var totalCount = await projectsQuery.CountAsync(cancellationToken);

        var pageSize = Math.Min(Math.Max(query.PageSize, 1), 100);
        var page = Math.Max(query.Page, 1);

        var projects = await projectsQuery
            .OrderByDescending(p => p.CreatedDateTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Get active spec names
        var activeSpecIds = projects
            .Where(p => p.ActiveSpecId.HasValue)
            .Select(p => p.ActiveSpecId.Value)
            .Distinct()
            .ToList();

        var activeSpecs = activeSpecIds.Count > 0
            ? await _specRepository.GetQueryableSet()
                .Where(s => activeSpecIds.Contains(s.Id))
                .Select(s => new { s.Id, s.Name })
                .ToListAsync(cancellationToken)
            : new();

        var specNameLookup = activeSpecs.ToDictionary(s => s.Id, s => s.Name);

        var items = projects.Select(p => new ProjectModel
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            BaseUrl = p.BaseUrl,
            Status = p.Status.ToString(),
            ActiveSpecId = p.ActiveSpecId,
            ActiveSpecName = p.ActiveSpecId.HasValue && specNameLookup.ContainsKey(p.ActiveSpecId.Value)
                ? specNameLookup[p.ActiveSpecId.Value]
                : null,
            CreatedDateTime = p.CreatedDateTime,
            UpdatedDateTime = p.UpdatedDateTime,
        }).ToList();

        return new PaginatedResult<ProjectModel>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
        };
    }
}
