using ClassifiedAds.Application;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Authorization;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.Identity.Entities;
using ClassifiedAds.Modules.Identity.Queries.Roles;
using ClassifiedAds.WebAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.WebAPI.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/api-docs")]
public class AdminApiDocumentationController : ControllerBase
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly Dispatcher _dispatcher;

    public AdminApiDocumentationController(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        Dispatcher dispatcher)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _dispatcher = dispatcher;
    }

    [Authorize(Permissions.GetProjects)]
    [HttpGet("projects")]
    public async Task<ActionResult<List<AdminApiDocumentationProjectModel>>> GetProjects(
        [FromQuery] ProjectStatus? status = null,
        [FromQuery] string search = null,
        [FromQuery] bool includeDeletedSpecs = false,
        CancellationToken ct = default)
    {
        var projectsQuery = _projectRepository.GetQueryableSet().AsNoTracking();

        if (status.HasValue)
        {
            projectsQuery = projectsQuery.Where(p => p.Status == status.Value);
        }

        var projects = await projectsQuery
            .OrderByDescending(p => p.CreatedDateTime)
            .ToListAsync(ct);

        if (projects.Count == 0)
        {
            return Ok(new List<AdminApiDocumentationProjectModel>());
        }

        var users = await _dispatcher.DispatchAsync(new GetUsersQuery { AsNoTracking = true }, ct);
        var userNameLookup = users.ToDictionary(x => x.Id, x => ResolveUserName(x));
        var userEmailLookup = users.ToDictionary(x => x.Id, x => ResolveUserEmail(x));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var trimmed = search.Trim().ToLower();
            projects = projects
                .Where(project =>
                {
                    var projectName = project.Name?.ToLower() ?? string.Empty;
                    if (projectName.Contains(trimmed))
                    {
                        return true;
                    }

                    userNameLookup.TryGetValue(project.OwnerId, out var ownerName);
                    userEmailLookup.TryGetValue(project.OwnerId, out var ownerEmail);

                    var normalizedName = ownerName?.ToLower() ?? string.Empty;
                    var normalizedEmail = ownerEmail?.ToLower() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(normalizedName) &&
                        string.IsNullOrWhiteSpace(normalizedEmail))
                    {
                        return false;
                    }

                    return normalizedName.Contains(trimmed) || normalizedEmail.Contains(trimmed);
                })
                .ToList();
        }

        if (projects.Count == 0)
        {
            return Ok(new List<AdminApiDocumentationProjectModel>());
        }

        var projectIds = projects.Select(p => p.Id).ToList();

        var specsQuery = _specRepository.GetQueryableSet().AsNoTracking()
            .Where(s => projectIds.Contains(s.ProjectId));

        if (!includeDeletedSpecs)
        {
            specsQuery = specsQuery.Where(s => !s.IsDeleted);
        }

        var specs = await specsQuery
            .OrderByDescending(s => s.CreatedDateTime)
            .ToListAsync(ct);

        var specIds = specs.Select(s => s.Id).ToList();
        var endpointCounts = specIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await _endpointRepository.GetQueryableSet().AsNoTracking()
                .Where(e => specIds.Contains(e.ApiSpecId))
                .GroupBy(e => e.ApiSpecId)
                .Select(g => new { SpecId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SpecId, x => x.Count, ct);

        var specsByProject = specs
            .GroupBy(s => s.ProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<AdminApiDocumentationProjectModel>();
        foreach (var project in projects)
        {
            specsByProject.TryGetValue(project.Id, out var projectSpecs);
            projectSpecs ??= new List<ApiSpecification>();

            var specModels = projectSpecs.Select(spec => new AdminApiDocumentationSpecModel
            {
                Id = spec.Id,
                ProjectId = spec.ProjectId,
                Name = spec.Name,
                SourceType = spec.SourceType.ToString(),
                Version = spec.Version,
                IsActive = spec.IsActive,
                ParseStatus = spec.ParseStatus.ToString(),
                ParsedAt = spec.ParsedAt,
                EndpointCount = endpointCounts.TryGetValue(spec.Id, out var count) ? count : 0,
                IsDeleted = spec.IsDeleted,
                CreatedDateTime = spec.CreatedDateTime,
                UpdatedDateTime = spec.UpdatedDateTime,
            }).ToList();

            result.Add(new AdminApiDocumentationProjectModel
            {
                Id = project.Id,
                OwnerId = project.OwnerId,
                OwnerName = ResolveUserName(userNameLookup, project.OwnerId),
                OwnerEmail = ResolveUserEmail(userEmailLookup, project.OwnerId),
                Name = project.Name,
                Description = project.Description,
                BaseUrl = project.BaseUrl,
                Status = project.Status.ToString(),
                ActiveSpecId = project.ActiveSpecId,
                ActiveSpecName = project.ActiveSpecId.HasValue
                    ? specModels.FirstOrDefault(x => x.Id == project.ActiveSpecId)?.Name
                    : null,
                TotalSpecifications = projectSpecs.Count,
                CreatedDateTime = project.CreatedDateTime,
                UpdatedDateTime = project.UpdatedDateTime,
                Specifications = specModels,
            });
        }

        return Ok(result);
    }

    private static string ResolveUserName(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.UserName))
        {
            return user.UserName;
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return user.Email;
        }

        return "Unknown";
    }

    private static string ResolveUserName(Dictionary<Guid, string> lookup, Guid userId)
    {
        if (lookup != null && lookup.TryGetValue(userId, out var name))
        {
            return name;
        }

        return "Unknown";
    }

    private static string ResolveUserEmail(User user)
    {
        return string.IsNullOrWhiteSpace(user.Email) ? string.Empty : user.Email;
    }

    private static string ResolveUserEmail(Dictionary<Guid, string> lookup, Guid userId)
    {
        if (lookup != null && lookup.TryGetValue(userId, out var owner))
        {
            return owner ?? string.Empty;
        }

        return string.Empty;
    }
}
