using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Services;

public class ApiEndpointParameterDetailService : IApiEndpointParameterDetailService
{
    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;

    public ApiEndpointParameterDetailService(
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointParameter, Guid> parameterRepository)
    {
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _parameterRepository = parameterRepository;
    }

    public async Task<IReadOnlyList<EndpointParameterDetailDto>> GetParameterDetailsAsync(
        Guid specificationId,
        IReadOnlyCollection<Guid> endpointIds,
        CancellationToken cancellationToken = default)
    {
        if (specificationId == Guid.Empty)
        {
            throw new ValidationException("SpecificationId là bắt buộc.");
        }

        var specification = await _specRepository.FirstOrDefaultAsync(
            _specRepository.GetQueryableSet().Where(x => x.Id == specificationId));

        if (specification == null)
        {
            throw new NotFoundException($"Không tìm thấy specification với mã '{specificationId}'.");
        }

        var normalizedIds = (endpointIds ?? Array.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .ToHashSet();

        var endpointQuery = _endpointRepository.GetQueryableSet()
            .Where(x => x.ApiSpecId == specificationId);

        if (normalizedIds.Count > 0)
        {
            endpointQuery = endpointQuery.Where(x => normalizedIds.Contains(x.Id));
        }

        var endpoints = await _endpointRepository.ToListAsync(endpointQuery);

        if (endpoints.Count == 0)
        {
            return Array.Empty<EndpointParameterDetailDto>();
        }

        var loadedEndpointIds = endpoints.Select(x => x.Id).ToList();

        var parameters = await _parameterRepository.ToListAsync(
            _parameterRepository.GetQueryableSet()
                .Where(x => loadedEndpointIds.Contains(x.EndpointId)));

        var paramsByEndpoint = parameters
            .GroupBy(x => x.EndpointId)
            .ToDictionary(x => x.Key, x => x.ToList());

        return endpoints
            .Select(endpoint => new EndpointParameterDetailDto
            {
                EndpointId = endpoint.Id,
                EndpointPath = endpoint.Path,
                EndpointHttpMethod = endpoint.HttpMethod.ToString(),
                Parameters = paramsByEndpoint.TryGetValue(endpoint.Id, out var endpointParams)
                    ? endpointParams.Select(p => new ParameterDetailDto
                    {
                        ParameterId = p.Id,
                        Name = p.Name,
                        Location = p.Location.ToString(),
                        DataType = p.DataType,
                        Format = p.Format,
                        IsRequired = p.IsRequired,
                        DefaultValue = p.DefaultValue,
                        Schema = p.Schema,
                        Examples = p.Examples,
                    }).ToList()
                    : Array.Empty<ParameterDetailDto>(),
            })
            .ToList();
    }
}
