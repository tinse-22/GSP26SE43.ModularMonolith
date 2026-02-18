using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Queries;

public class GetEndpointQuery : IQuery<EndpointDetailModel>
{
    public Guid ProjectId { get; set; }

    public Guid SpecId { get; set; }

    public Guid EndpointId { get; set; }

    public Guid OwnerId { get; set; }
}

public class GetEndpointQueryHandler : IQueryHandler<GetEndpointQuery, EndpointDetailModel>
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;
    private readonly IRepository<EndpointResponse, Guid> _responseRepository;
    private readonly IRepository<EndpointSecurityReq, Guid> _securityReqRepository;
    private readonly Services.IPathParameterTemplateService _pathParamService;

    public GetEndpointQueryHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointParameter, Guid> parameterRepository,
        IRepository<EndpointResponse, Guid> responseRepository,
        IRepository<EndpointSecurityReq, Guid> securityReqRepository,
        Services.IPathParameterTemplateService pathParamService)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _parameterRepository = parameterRepository;
        _responseRepository = responseRepository;
        _securityReqRepository = securityReqRepository;
        _pathParamService = pathParamService;
    }

    public async Task<EndpointDetailModel> HandleAsync(GetEndpointQuery query, CancellationToken cancellationToken = default)
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

        // Load endpoint
        var endpoint = await _endpointRepository.FirstOrDefaultAsync(
            _endpointRepository.GetQueryableSet().Where(e => e.Id == query.EndpointId && e.ApiSpecId == query.SpecId));

        if (endpoint == null)
        {
            throw new NotFoundException($"Không tìm thấy endpoint với mã '{query.EndpointId}'.");
        }

        // Load children
        var parameters = await _parameterRepository.GetQueryableSet()
            .Where(p => p.EndpointId == endpoint.Id)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var responses = await _responseRepository.GetQueryableSet()
            .Where(r => r.EndpointId == endpoint.Id)
            .OrderBy(r => r.StatusCode)
            .ToListAsync(cancellationToken);

        var securityReqs = await _securityReqRepository.GetQueryableSet()
            .Where(sr => sr.EndpointId == endpoint.Id)
            .ToListAsync(cancellationToken);

        var model = new EndpointDetailModel
        {
            Id = endpoint.Id,
            ApiSpecId = endpoint.ApiSpecId,
            HttpMethod = endpoint.HttpMethod.ToString(),
            Path = endpoint.Path,
            OperationId = endpoint.OperationId,
            Summary = endpoint.Summary,
            Description = endpoint.Description,
            Tags = endpoint.Tags,
            IsDeprecated = endpoint.IsDeprecated,
            CreatedDateTime = endpoint.CreatedDateTime,
            UpdatedDateTime = endpoint.UpdatedDateTime,
            Parameters = parameters.Select(p => new ParameterModel
            {
                Id = p.Id,
                Name = p.Name,
                Location = p.Location.ToString(),
                DataType = p.DataType,
                Format = p.Format,
                IsRequired = p.IsRequired,
                DefaultValue = p.DefaultValue,
                Schema = p.Schema,
                Examples = p.Examples,
            }).ToList(),
            Responses = responses.Select(r => new ResponseModel
            {
                Id = r.Id,
                StatusCode = r.StatusCode,
                Description = r.Description,
                Schema = r.Schema,
                Examples = r.Examples,
                Headers = r.Headers,
            }).ToList(),
            SecurityRequirements = securityReqs.Select(sr => new SecurityReqModel
            {
                Id = sr.Id,
                SecurityType = sr.SecurityType.ToString(),
                SchemeName = sr.SchemeName,
                Scopes = sr.Scopes,
            }).ToList(),
        };

        // Resolve URL using DefaultValue → Examples fallback
        var pathParamValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters.Where(p => p.Location == ParameterLocation.Path))
        {
            if (!string.IsNullOrEmpty(p.DefaultValue))
            {
                pathParamValues[p.Name] = p.DefaultValue;
            }
            else if (!string.IsNullOrEmpty(p.Examples))
            {
                try
                {
                    var examples = JsonSerializer.Deserialize<List<string>>(p.Examples);
                    if (examples?.Count > 0)
                    {
                        pathParamValues[p.Name] = examples[0];
                    }
                }
                catch
                {
                }
            }
        }

        var resolved = _pathParamService.ResolveUrl(endpoint.Path, pathParamValues);
        model.ResolvedUrl = resolved.IsFullyResolved ? resolved.ResolvedUrl : null;

        return model;
    }
}
