using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Queries;

public class GetResolvedUrlQuery : IQuery<ResolvedUrlResult>
{
    public Guid ProjectId { get; set; }

    public Guid SpecId { get; set; }

    public Guid EndpointId { get; set; }

    public Guid OwnerId { get; set; }

    public Dictionary<string, string> ParameterValues { get; set; }
}

public class GetResolvedUrlQueryHandler : IQueryHandler<GetResolvedUrlQuery, ResolvedUrlResult>
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;
    private readonly IPathParameterTemplateService _pathParamService;

    public GetResolvedUrlQueryHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointParameter, Guid> parameterRepository,
        IPathParameterTemplateService pathParamService)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _parameterRepository = parameterRepository;
        _pathParamService = pathParamService;
    }

    public async Task<ResolvedUrlResult> HandleAsync(GetResolvedUrlQuery query, CancellationToken cancellationToken = default)
    {
        // 1. Verify project ownership
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

        // 2. Verify spec belongs to project
        var spec = await _specRepository.FirstOrDefaultAsync(
            _specRepository.GetQueryableSet().Where(s => s.Id == query.SpecId && s.ProjectId == query.ProjectId));

        if (spec == null)
        {
            throw new NotFoundException($"Không tìm thấy specification với mã '{query.SpecId}'.");
        }

        // 3. Load endpoint
        var endpoint = await _endpointRepository.FirstOrDefaultAsync(
            _endpointRepository.GetQueryableSet().Where(e => e.Id == query.EndpointId && e.ApiSpecId == query.SpecId));

        if (endpoint == null)
        {
            throw new NotFoundException($"Không tìm thấy endpoint với mã '{query.EndpointId}'.");
        }

        // 4. Build parameter values with fallback (explicit values take precedence)
        var parameterValues = query.ParameterValues == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(query.ParameterValues, StringComparer.OrdinalIgnoreCase);

        var pathParams = await _parameterRepository.GetQueryableSet()
            .Where(p => p.EndpointId == query.EndpointId && p.Location == ParameterLocation.Path)
            .ToListAsync(cancellationToken);

        foreach (var p in pathParams)
        {
            if (parameterValues.TryGetValue(p.Name, out var explicitValue) &&
                !string.IsNullOrWhiteSpace(explicitValue))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(p.DefaultValue))
            {
                parameterValues[p.Name] = p.DefaultValue;
                continue;
            }

            if (!string.IsNullOrWhiteSpace(p.Examples))
            {
                try
                {
                    var examples = JsonSerializer.Deserialize<List<string>>(p.Examples);
                    var firstExample = examples?.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
                    if (!string.IsNullOrWhiteSpace(firstExample))
                    {
                        parameterValues[p.Name] = firstExample;
                    }
                }
                catch
                {
                    // Ignore malformed Examples JSON and continue with unresolved value
                }
            }
        }

        // 5. Resolve URL
        var result = _pathParamService.ResolveUrl(endpoint.Path, parameterValues);
        return result;
    }
}
