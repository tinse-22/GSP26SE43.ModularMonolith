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

public class ApiEndpointMetadataService : IApiEndpointMetadataService
{
    private static readonly string[] AuthKeywords =
    {
        "/auth",
        "/token",
        "/login",
        "/signin",
        "/oauth",
        "/refresh",
        "/logout",
    };

    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointSecurityReq, Guid> _securityRequirementRepository;

    public ApiEndpointMetadataService(
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointSecurityReq, Guid> securityRequirementRepository)
    {
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _securityRequirementRepository = securityRequirementRepository;
    }

    public async Task<IReadOnlyList<ApiEndpointMetadataDto>> GetEndpointMetadataAsync(
        Guid specificationId,
        IReadOnlyCollection<Guid> selectedEndpointIds = null,
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

        var normalizedSelectedEndpointIds = NormalizeEndpointIds(selectedEndpointIds);

        var endpointQuery = _endpointRepository.GetQueryableSet()
            .Where(x => x.ApiSpecId == specificationId);

        if (normalizedSelectedEndpointIds.Count > 0)
        {
            endpointQuery = endpointQuery.Where(x => normalizedSelectedEndpointIds.Contains(x.Id));
        }

        var endpoints = await _endpointRepository.ToListAsync(endpointQuery);
        EnsureSelectedEndpointsExist(normalizedSelectedEndpointIds, endpoints);

        if (endpoints.Count == 0)
        {
            return Array.Empty<ApiEndpointMetadataDto>();
        }

        var endpointIds = endpoints.Select(x => x.Id).ToList();
        var securityRequirements = await _securityRequirementRepository.ToListAsync(
            _securityRequirementRepository.GetQueryableSet()
                .Where(x => endpointIds.Contains(x.EndpointId)));

        var securedEndpointIds = securityRequirements
            .Select(x => x.EndpointId)
            .ToHashSet();

        var postEndpointsByResourcePath = endpoints
            .Where(x => x.HttpMethod == Entities.HttpMethod.POST)
            .GroupBy(x => GetCollectionPath(x.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).First(),
                StringComparer.OrdinalIgnoreCase);

        var orderedEndpoints = endpoints
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.HttpMethod.ToString(), StringComparer.OrdinalIgnoreCase)
            .ToList();

        return orderedEndpoints
            .Select(endpoint => new ApiEndpointMetadataDto
            {
                EndpointId = endpoint.Id,
                HttpMethod = endpoint.HttpMethod.ToString(),
                Path = endpoint.Path,
                OperationId = endpoint.OperationId,
                IsAuthRelated = IsAuthRelated(endpoint, securedEndpointIds.Contains(endpoint.Id)),
                DependsOnEndpointIds = BuildDependencies(endpoint, postEndpointsByResourcePath),
            })
            .ToList();
    }

    private static HashSet<Guid> NormalizeEndpointIds(IReadOnlyCollection<Guid> endpointIds)
    {
        if (endpointIds == null || endpointIds.Count == 0)
        {
            return new HashSet<Guid>();
        }

        return endpointIds
            .Where(x => x != Guid.Empty)
            .ToHashSet();
    }

    private static void EnsureSelectedEndpointsExist(IReadOnlyCollection<Guid> selectedEndpointIds, IReadOnlyCollection<ApiEndpoint> loadedEndpoints)
    {
        if (selectedEndpointIds == null || selectedEndpointIds.Count == 0)
        {
            return;
        }

        var loadedIds = loadedEndpoints.Select(x => x.Id).ToHashSet();
        var missingIds = selectedEndpointIds.Where(x => !loadedIds.Contains(x)).ToList();
        if (missingIds.Count > 0)
        {
            throw new ValidationException($"Một số endpoint không tồn tại trong specification: {string.Join(", ", missingIds)}");
        }
    }

    private static bool IsAuthRelated(ApiEndpoint endpoint, bool endpointRequiresSecurity)
    {
        if (endpoint == null)
        {
            return false;
        }

        if (endpointRequiresSecurity)
        {
            return false;
        }

        var signature = $"{endpoint.Path} {endpoint.OperationId} {endpoint.Summary}".ToLowerInvariant();
        return AuthKeywords.Any(signature.Contains);
    }

    private static IReadOnlyCollection<Guid> BuildDependencies(
        ApiEndpoint endpoint,
        IReadOnlyDictionary<string, ApiEndpoint> postEndpointsByResourcePath)
    {
        if (endpoint == null || endpoint.HttpMethod == Entities.HttpMethod.POST || string.IsNullOrWhiteSpace(endpoint.Path))
        {
            return Array.Empty<Guid>();
        }

        if (!endpoint.Path.Contains('{'))
        {
            return Array.Empty<Guid>();
        }

        var resourcePath = GetCollectionPath(endpoint.Path);
        if (postEndpointsByResourcePath.TryGetValue(resourcePath, out var dependency) && dependency.Id != endpoint.Id)
        {
            return new[] { dependency.Id };
        }

        return Array.Empty<Guid>();
    }

    private static string GetCollectionPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var segments = path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        if (segments.Count == 0)
        {
            return "/";
        }

        if (segments[^1].Contains('{'))
        {
            segments.RemoveAt(segments.Count - 1);
        }

        return "/" + string.Join("/", segments).ToLowerInvariant();
    }
}
