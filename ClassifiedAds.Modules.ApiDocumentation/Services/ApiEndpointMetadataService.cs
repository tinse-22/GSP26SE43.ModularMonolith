using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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

    private static readonly HashSet<string> IgnoredDependencyTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "api",
        "v1",
        "v2",
        "v3",
        "id",
        "ids",
        "by",
        "for",
        "of",
        "the",
        "a",
        "an",
    };

    private static readonly Regex SchemaRefRegex = new(
        @"#/(?:components/schemas|definitions)/(?<name>[A-Za-z0-9_.-]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PathParameterRegex = new(
        @"\{(?<name>[^{}]+)\}",
        RegexOptions.Compiled);

    private static readonly Regex IdentifierSplitRegex = new(
        @"(?<=[a-z0-9])(?=[A-Z])|[_\-\s\./\[\]\{\}]",
        RegexOptions.Compiled);

    private readonly IRepository<ApiSpecification, Guid> _specRepository;
    private readonly IRepository<ApiEndpoint, Guid> _endpointRepository;
    private readonly IRepository<EndpointParameter, Guid> _parameterRepository;
    private readonly IRepository<EndpointResponse, Guid> _responseRepository;
    private readonly IRepository<EndpointSecurityReq, Guid> _securityRequirementRepository;

    public ApiEndpointMetadataService(
        IRepository<ApiSpecification, Guid> specRepository,
        IRepository<ApiEndpoint, Guid> endpointRepository,
        IRepository<EndpointParameter, Guid> parameterRepository,
        IRepository<EndpointResponse, Guid> responseRepository,
        IRepository<EndpointSecurityReq, Guid> securityRequirementRepository)
    {
        _specRepository = specRepository;
        _endpointRepository = endpointRepository;
        _parameterRepository = parameterRepository;
        _responseRepository = responseRepository;
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
        var endpointIdSet = endpointIds.ToHashSet();
        var securityRequirements = await _securityRequirementRepository.ToListAsync(
            _securityRequirementRepository.GetQueryableSet()
                .Where(x => endpointIds.Contains(x.EndpointId)));

        var endpointParameters = await _parameterRepository.ToListAsync(
            _parameterRepository.GetQueryableSet()
                .Where(x => endpointIds.Contains(x.EndpointId)));

        var endpointResponses = await _responseRepository.ToListAsync(
            _responseRepository.GetQueryableSet()
                .Where(x => endpointIds.Contains(x.EndpointId)));

        var securedEndpointIds = securityRequirements
            .Select(x => x.EndpointId)
            .ToHashSet();

        var endpointParametersById = endpointParameters
            .GroupBy(x => x.EndpointId)
            .ToDictionary(x => x.Key, x => (IReadOnlyCollection<EndpointParameter>)x.ToList());

        var endpointResponsesById = endpointResponses
            .GroupBy(x => x.EndpointId)
            .ToDictionary(x => x.Key, x => (IReadOnlyCollection<EndpointResponse>)x.ToList());

        var isAuthRelatedByEndpointId = endpoints.ToDictionary(
            x => x.Id,
            x => IsAuthRelated(x, securedEndpointIds.Contains(x.Id)));

        var authBootstrapEndpointIds = endpoints
            .Where(x => isAuthRelatedByEndpointId[x.Id])
            .OrderBy(x => GetMethodWeight(x.HttpMethod))
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)
            .ToList();

        var postEndpointsByResourcePath = endpoints
            .Where(x => x.HttpMethod == Entities.HttpMethod.POST)
            .GroupBy(x => GetCollectionPath(x.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => x.OrderBy(e => e.Path, StringComparer.OrdinalIgnoreCase).First(),
                StringComparer.OrdinalIgnoreCase);

        var parameterSchemaRefsByEndpointId = endpoints.ToDictionary(
            x => x.Id,
            x => ExtractSchemaReferences(
                endpointParametersById.TryGetValue(x.Id, out var parameters)
                    ? parameters.Select(p => p.Schema)
                    : Array.Empty<string>()));

        var responseSchemaRefsByEndpointId = endpoints.ToDictionary(
            x => x.Id,
            x => ExtractSchemaReferences(
                endpointResponsesById.TryGetValue(x.Id, out var responses)
                    ? responses.Where(r => r.StatusCode >= 200 && r.StatusCode <= 299).Select(r => r.Schema)
                    : Array.Empty<string>()));

        var responseProducersBySchemaRef = BuildResponseProducersBySchemaRef(
            endpoints,
            responseSchemaRefsByEndpointId);

        var dependencyProducersByResourceToken = BuildPostProducersByResourceToken(endpoints);

        var parameterTokensByEndpointId = endpoints.ToDictionary(
            x => x.Id,
            x => ExtractParameterTokens(
                x.Path,
                endpointParametersById.TryGetValue(x.Id, out var parameters)
                    ? parameters
                    : Array.Empty<EndpointParameter>()));

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
                IsAuthRelated = isAuthRelatedByEndpointId[endpoint.Id],
                DependsOnEndpointIds = BuildDependencies(
                    endpoint,
                    endpointIdSet,
                    postEndpointsByResourcePath,
                    parameterSchemaRefsByEndpointId,
                    responseProducersBySchemaRef,
                    parameterTokensByEndpointId,
                    dependencyProducersByResourceToken,
                    authBootstrapEndpointIds,
                    securedEndpointIds),

                // KAT paper (Schema-Schema dependency): populate schema data for advanced analysis.
                ParameterSchemaRefs = parameterSchemaRefsByEndpointId.TryGetValue(endpoint.Id, out var paramRefs)
                    ? paramRefs.ToList()
                    : Array.Empty<string>(),
                ResponseSchemaRefs = responseSchemaRefsByEndpointId.TryGetValue(endpoint.Id, out var respRefs)
                    ? respRefs.ToList()
                    : Array.Empty<string>(),
                ParameterSchemaPayloads = endpointParametersById.TryGetValue(endpoint.Id, out var paramEntities)
                    ? paramEntities.Where(p => !string.IsNullOrWhiteSpace(p.Schema)).Select(p => p.Schema).ToList()
                    : Array.Empty<string>(),
                ResponseSchemaPayloads = endpointResponsesById.TryGetValue(endpoint.Id, out var respEntities)
                    ? respEntities.Where(r => r.StatusCode >= 200 && r.StatusCode <= 299 && !string.IsNullOrWhiteSpace(r.Schema)).Select(r => r.Schema).ToList()
                    : Array.Empty<string>(),
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

        var signature = $"{endpoint.Path} {endpoint.OperationId} {endpoint.Summary} {endpoint.Description}".ToLowerInvariant();
        return AuthKeywords.Any(signature.Contains);
    }

    private static IReadOnlyCollection<Guid> BuildDependencies(
        ApiEndpoint endpoint,
        ISet<Guid> endpointIdSet,
        IReadOnlyDictionary<string, ApiEndpoint> postEndpointsByResourcePath,
        IReadOnlyDictionary<Guid, HashSet<string>> parameterSchemaRefsByEndpointId,
        IReadOnlyDictionary<string, IReadOnlyList<ApiEndpoint>> responseProducersBySchemaRef,
        IReadOnlyDictionary<Guid, HashSet<string>> parameterTokensByEndpointId,
        IReadOnlyDictionary<string, IReadOnlyList<ApiEndpoint>> dependencyProducersByResourceToken,
        IReadOnlyList<Guid> authBootstrapEndpointIds,
        ISet<Guid> securedEndpointIds)
    {
        if (endpoint == null)
        {
            return Array.Empty<Guid>();
        }

        var dependencyIds = new HashSet<Guid>();

        // Rule 1: endpoint with path item id should depend on resource producers (POST /resource).
        if (endpoint.HttpMethod != Entities.HttpMethod.POST
            && !string.IsNullOrWhiteSpace(endpoint.Path)
            && endpoint.Path.Contains('{'))
        {
            var resourcePath = GetCollectionPath(endpoint.Path);
            if (postEndpointsByResourcePath.TryGetValue(resourcePath, out var dependency) && dependency.Id != endpoint.Id)
            {
                dependencyIds.Add(dependency.Id);
            }
        }

        // Rule 2: operation-schema dependency from parameter schema refs to response schema refs.
        if (parameterSchemaRefsByEndpointId.TryGetValue(endpoint.Id, out var parameterSchemaRefs))
        {
            foreach (var schemaRef in parameterSchemaRefs)
            {
                if (!responseProducersBySchemaRef.TryGetValue(schemaRef, out var producers))
                {
                    continue;
                }

                var candidateProducers = producers
                    .Where(x => x.Id != endpoint.Id)
                    .ToList();

                if (candidateProducers.Count == 0)
                {
                    continue;
                }

                var prioritizedPostProducers = candidateProducers
                    .Where(x => x.HttpMethod == Entities.HttpMethod.POST)
                    .ToList();
                if (prioritizedPostProducers.Count > 0)
                {
                    foreach (var producer in prioritizedPostProducers)
                    {
                        dependencyIds.Add(producer.Id);
                    }

                    continue;
                }

                var prioritizedCollectionGetProducers = candidateProducers
                    .Where(x => x.HttpMethod == Entities.HttpMethod.GET && !HasPathParameter(x.Path))
                    .ToList();
                if (prioritizedCollectionGetProducers.Count > 0)
                {
                    foreach (var producer in prioritizedCollectionGetProducers)
                    {
                        dependencyIds.Add(producer.Id);
                    }

                    continue;
                }

                dependencyIds.Add(candidateProducers[0].Id);
            }
        }

        // Rule 3: semantic token dependency between input identifier tokens and resource producers.
        if (parameterTokensByEndpointId.TryGetValue(endpoint.Id, out var parameterTokens))
        {
            foreach (var token in parameterTokens)
            {
                if (!dependencyProducersByResourceToken.TryGetValue(token, out var producers))
                {
                    continue;
                }

                foreach (var producer in producers.Where(x => x.Id != endpoint.Id))
                {
                    dependencyIds.Add(producer.Id);
                }
            }
        }

        // Rule 4: secured endpoint should bootstrap auth token first.
        if (securedEndpointIds.Contains(endpoint.Id))
        {
            var authDependencyId = authBootstrapEndpointIds.FirstOrDefault(x => x != endpoint.Id);
            if (authDependencyId != Guid.Empty)
            {
                dependencyIds.Add(authDependencyId);
            }
        }

        // Keep only dependencies inside selected endpoint set for deterministic order proposal.
        return dependencyIds
            .Where(endpointIdSet.Contains)
            .OrderBy(x => x)
            .ToList();
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

    private static bool HasPathParameter(string path)
    {
        return !string.IsNullOrWhiteSpace(path) && PathParameterRegex.IsMatch(path);
    }

    private static HashSet<string> ExtractSchemaReferences(IEnumerable<string> schemaPayloads)
    {
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (schemaPayloads == null)
        {
            return references;
        }

        foreach (var schemaPayload in schemaPayloads.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            foreach (Match match in SchemaRefRegex.Matches(schemaPayload))
            {
                var schemaName = match.Groups["name"].Value?.Trim();
                if (!string.IsNullOrWhiteSpace(schemaName))
                {
                    references.Add(schemaName);
                }
            }
        }

        return references;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ApiEndpoint>> BuildResponseProducersBySchemaRef(
        IReadOnlyCollection<ApiEndpoint> endpoints,
        IReadOnlyDictionary<Guid, HashSet<string>> responseSchemaRefsByEndpointId)
    {
        var schemaRefToProducers = new Dictionary<string, List<ApiEndpoint>>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in endpoints)
        {
            if (!responseSchemaRefsByEndpointId.TryGetValue(endpoint.Id, out var schemaRefs) || schemaRefs.Count == 0)
            {
                continue;
            }

            foreach (var schemaRef in schemaRefs)
            {
                if (!schemaRefToProducers.TryGetValue(schemaRef, out var producers))
                {
                    producers = new List<ApiEndpoint>();
                    schemaRefToProducers[schemaRef] = producers;
                }

                producers.Add(endpoint);
            }
        }

        return schemaRefToProducers.ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<ApiEndpoint>)x.Value
                .GroupBy(endpoint => endpoint.Id)
                .Select(group => group.First())
                .OrderBy(endpoint => GetMethodWeight(endpoint.HttpMethod))
                .ThenBy(endpoint => HasPathParameter(endpoint.Path) ? 1 : 0)
                .ThenBy(endpoint => endpoint.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(endpoint => endpoint.Id)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ApiEndpoint>> BuildPostProducersByResourceToken(
        IReadOnlyCollection<ApiEndpoint> endpoints)
    {
        var tokenMap = new Dictionary<string, List<ApiEndpoint>>(StringComparer.OrdinalIgnoreCase);

        foreach (var endpoint in endpoints.Where(x => x.HttpMethod == Entities.HttpMethod.POST))
        {
            foreach (var token in ExtractResourceTokens(endpoint.Path))
            {
                if (!tokenMap.TryGetValue(token, out var producers))
                {
                    producers = new List<ApiEndpoint>();
                    tokenMap[token] = producers;
                }

                producers.Add(endpoint);
            }
        }

        return tokenMap.ToDictionary(
            x => x.Key,
            x => (IReadOnlyList<ApiEndpoint>)x.Value
                .GroupBy(endpoint => endpoint.Id)
                .Select(group => group.First())
                .OrderBy(endpoint => endpoint.Path, StringComparer.OrdinalIgnoreCase)
                .ThenBy(endpoint => endpoint.Id)
                .ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> ExtractParameterTokens(
        string path,
        IReadOnlyCollection<EndpointParameter> parameters)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var parameterName in ExtractPathParameterNames(path))
        {
            AddIdentifierTokens(parameterName, tokens);
        }

        if (parameters != null)
        {
            foreach (var parameter in parameters)
            {
                AddIdentifierTokens(parameter.Name, tokens);
            }
        }

        return tokens;
    }

    private static IEnumerable<string> ExtractPathParameterNames(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            yield break;
        }

        foreach (Match match in PathParameterRegex.Matches(path))
        {
            var parameterName = match.Groups["name"].Value?.Trim();
            if (!string.IsNullOrWhiteSpace(parameterName))
            {
                yield return parameterName;
            }
        }
    }

    private static IEnumerable<string> ExtractResourceTokens(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<string>();
        }

        return path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(segment => !segment.Contains('{'))
            .SelectMany(segment => SplitIdentifierTokens(segment))
            .Select(token => token.Trim())
            .Where(token => token.Length > 1 && !IgnoredDependencyTokens.Contains(token))
            .Select(token => token.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> SplitIdentifierTokens(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return IdentifierSplitRegex
            .Split(value)
            .Where(token => !string.IsNullOrWhiteSpace(token));
    }

    private static void AddIdentifierTokens(string value, ISet<string> target)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        foreach (var token in SplitIdentifierTokens(value))
        {
            var normalizedToken = token.Trim().ToLowerInvariant();
            if (normalizedToken.Length > 1 && !IgnoredDependencyTokens.Contains(normalizedToken))
            {
                target.Add(normalizedToken);
            }
        }

        var trimmedValue = value.Trim();
        if (trimmedValue.EndsWith("Ids", StringComparison.OrdinalIgnoreCase) && trimmedValue.Length > 3)
        {
            foreach (var token in SplitIdentifierTokens(trimmedValue[..^3]))
            {
                var normalizedToken = token.Trim().ToLowerInvariant();
                if (normalizedToken.Length > 1 && !IgnoredDependencyTokens.Contains(normalizedToken))
                {
                    target.Add(normalizedToken);
                }
            }
        }
        else if (trimmedValue.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && trimmedValue.Length > 2)
        {
            foreach (var token in SplitIdentifierTokens(trimmedValue[..^2]))
            {
                var normalizedToken = token.Trim().ToLowerInvariant();
                if (normalizedToken.Length > 1 && !IgnoredDependencyTokens.Contains(normalizedToken))
                {
                    target.Add(normalizedToken);
                }
            }
        }
    }

    private static int GetMethodWeight(Entities.HttpMethod method)
    {
        return method switch
        {
            Entities.HttpMethod.POST => 1,
            Entities.HttpMethod.PUT => 2,
            Entities.HttpMethod.PATCH => 3,
            Entities.HttpMethod.GET => 4,
            Entities.HttpMethod.DELETE => 5,
            Entities.HttpMethod.OPTIONS => 6,
            Entities.HttpMethod.HEAD => 7,
            _ => int.MaxValue,
        };
    }
}
