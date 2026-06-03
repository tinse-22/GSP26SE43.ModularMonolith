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

public class GetEndpointsQuery : IQuery<List<EndpointDetailModel>>
{
    public Guid ProjectId { get; set; }

    public Guid SpecId { get; set; }

    public Guid OwnerId { get; set; }
}

public class GetEndpointsQueryHandler : IQueryHandler<GetEndpointsQuery, List<EndpointDetailModel>>
{
    private readonly IRepository<Project, Guid> _projectRepository;
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;
    private readonly IRepository<EndpointResponse, Guid> _responseRepository;
    private readonly IRepository<EndpointSecurityReq, Guid> _securityReqRepository;

    public GetEndpointsQueryHandler(
        IRepository<Project, Guid> projectRepository,
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointParameter, Guid> parameterRepository,
        IRepository<EndpointResponse, Guid> responseRepository,
        IRepository<EndpointSecurityReq, Guid> securityReqRepository)
    {
        _projectRepository = projectRepository;
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _parameterRepository = parameterRepository;
        _responseRepository = responseRepository;
        _securityReqRepository = securityReqRepository;
    }

    public async Task<List<EndpointDetailModel>> HandleAsync(GetEndpointsQuery query, CancellationToken cancellationToken = default)
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

        var endpointIds = endpoints.Select(e => e.Id).ToList();

        var parametersByEndpointId = await _parameterRepository.GetQueryableSet()
            .Where(p => endpointIds.Contains(p.EndpointId))
            .OrderBy(p => p.Name)
            .GroupBy(p => p.EndpointId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

        var responsesByEndpointId = await _responseRepository.GetQueryableSet()
            .Where(r => endpointIds.Contains(r.EndpointId))
            .OrderBy(r => r.StatusCode)
            .GroupBy(r => r.EndpointId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

        var securityReqsByEndpointId = await _securityReqRepository.GetQueryableSet()
            .Where(sr => endpointIds.Contains(sr.EndpointId))
            .GroupBy(sr => sr.EndpointId)
            .ToDictionaryAsync(g => g.Key, g => g.ToList(), cancellationToken);

        return endpoints.Select(e =>
        {
            parametersByEndpointId.TryGetValue(e.Id, out var parameters);
            responsesByEndpointId.TryGetValue(e.Id, out var responses);
            securityReqsByEndpointId.TryGetValue(e.Id, out var securityReqs);

            return new EndpointDetailModel
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
                Parameters = (parameters ?? new List<EndpointParameter>()).Select(p => new ParameterModel
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
                Responses = (responses ?? new List<EndpointResponse>()).Select(r => new ResponseModel
                {
                    Id = r.Id,
                    StatusCode = r.StatusCode,
                    Description = r.Description,
                    Schema = r.Schema,
                    Examples = r.Examples,
                    Headers = r.Headers,
                }).ToList(),
                SecurityRequirements = (securityReqs ?? new List<EndpointSecurityReq>()).Select(sr => new SecurityReqModel
                {
                    Id = sr.Id,
                    SecurityType = sr.SecurityType.ToString(),
                    SchemeName = sr.SchemeName,
                    Scopes = sr.Scopes,
                }).ToList(),
            };
        }).ToList();
    }
}
