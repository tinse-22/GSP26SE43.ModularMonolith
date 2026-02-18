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
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Queries;

public class GetPathParamMutationsQuery : IQuery<PathParamMutationsResult>
{
    public Guid ProjectId { get; set; }

    public Guid SpecId { get; set; }

    public Guid EndpointId { get; set; }

    public Guid OwnerId { get; set; }
}

public class GetPathParamMutationsQueryHandler : IQueryHandler<GetPathParamMutationsQuery, PathParamMutationsResult>
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;
    private readonly IPathParameterTemplateService _pathParamService;

    public GetPathParamMutationsQueryHandler(
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

    public async Task<PathParamMutationsResult> HandleAsync(GetPathParamMutationsQuery query, CancellationToken cancellationToken = default)
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

        // 4. Load path parameters from DB
        var pathParams = await _parameterRepository.GetQueryableSet()
            .Where(p => p.EndpointId == query.EndpointId && p.Location == ParameterLocation.Path)
            .ToListAsync(cancellationToken);

        if (pathParams.Count == 0)
        {
            return new PathParamMutationsResult
            {
                EndpointId = endpoint.Id,
                TemplatePath = endpoint.Path,
                TotalMutations = 0,
                ParameterMutations = new List<ParameterMutationGroup>(),
            };
        }

        // 5. Build default values dictionary for other params
        var defaultValues = pathParams.ToDictionary(
            p => p.Name,
            p => p.DefaultValue ?? string.Empty,
            StringComparer.OrdinalIgnoreCase);

        // 6. For each path param, generate mutations and resolve URLs
        var groups = new List<ParameterMutationGroup>();

        foreach (var param in pathParams)
        {
            var mutations = _pathParamService.GenerateMutations(
                param.Name, param.DataType, param.Format, param.DefaultValue);

            var mutationsWithUrl = mutations.Select(m =>
            {
                // Build param values: mutated value for current param, defaults for others
                var values = new Dictionary<string, string>(defaultValues);
                values[param.Name] = m.Value;

                var resolved = _pathParamService.ResolveUrl(endpoint.Path, values);

                return new PathParameterMutationWithUrl
                {
                    MutationType = m.MutationType,
                    Label = m.Label,
                    Value = m.Value,
                    ExpectedStatusCode = m.ExpectedStatusCode,
                    Description = m.Description,
                    ResolvedUrl = resolved.ResolvedUrl,
                };
            }).ToList();

            groups.Add(new ParameterMutationGroup
            {
                ParameterName = param.Name,
                DataType = param.DataType ?? "string",
                Format = param.Format ?? string.Empty,
                OriginalValue = param.DefaultValue ?? string.Empty,
                Mutations = mutationsWithUrl,
            });
        }

        return new PathParamMutationsResult
        {
            EndpointId = endpoint.Id,
            TemplatePath = endpoint.Path,
            TotalMutations = groups.Sum(g => g.Mutations.Count),
            ParameterMutations = groups,
        };
    }
}
