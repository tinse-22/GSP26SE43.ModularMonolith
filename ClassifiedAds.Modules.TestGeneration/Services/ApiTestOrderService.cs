using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public class ApiTestOrderService : IApiTestOrderService
{
    private static readonly IReadOnlyDictionary<string, int> MethodWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["POST"] = 1,
        ["PUT"] = 2,
        ["PATCH"] = 3,
        ["GET"] = 4,
        ["DELETE"] = 5,
        ["OPTIONS"] = 6,
        ["HEAD"] = 7,
    };

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IApiEndpointMetadataService _endpointMetadataService;

    public ApiTestOrderService(IApiEndpointMetadataService endpointMetadataService)
    {
        _endpointMetadataService = endpointMetadataService;
    }

    public async Task<IReadOnlyList<ApiOrderItemModel>> BuildProposalOrderAsync(
        Guid suiteId,
        Guid specificationId,
        IReadOnlyCollection<Guid> selectedEndpointIds,
        CancellationToken cancellationToken = default)
    {
        if (specificationId == Guid.Empty)
        {
            throw new ValidationException("SpecificationId là bắt buộc.");
        }

        var endpoints = await _endpointMetadataService.GetEndpointMetadataAsync(
            specificationId,
            selectedEndpointIds,
            cancellationToken);

        if (endpoints == null || endpoints.Count == 0)
        {
            throw new ValidationException("Không tìm thấy endpoint phù hợp để tạo đề xuất thứ tự API.");
        }

        return endpoints
            .OrderBy(x => x.IsAuthRelated ? 0 : 1)
            .ThenBy(x => x.DependsOnEndpointIds?.Count ?? 0)
            .ThenBy(x => GetMethodWeight(x.HttpMethod))
            .ThenBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.EndpointId)
            .Select((endpoint, index) => new ApiOrderItemModel
            {
                EndpointId = endpoint.EndpointId,
                HttpMethod = endpoint.HttpMethod,
                Path = endpoint.Path,
                OrderIndex = index + 1,
                DependsOnEndpointIds = endpoint.DependsOnEndpointIds?.ToList() ?? new List<Guid>(),
                ReasonCodes = BuildReasonCodes(endpoint.IsAuthRelated, endpoint.DependsOnEndpointIds?.Count ?? 0),
                IsAuthRelated = endpoint.IsAuthRelated,
            })
            .ToList();
    }

    public IReadOnlyList<Guid> ValidateReorderedEndpointSet(
        IReadOnlyList<ApiOrderItemModel> proposedOrder,
        IReadOnlyCollection<Guid> orderedEndpointIds)
    {
        if (proposedOrder == null || proposedOrder.Count == 0)
        {
            throw new ValidationException($"{TestOrderReasonCodes.InvalidOrderSet}: ProposedOrder không hợp lệ.");
        }

        if (orderedEndpointIds == null || orderedEndpointIds.Count == 0)
        {
            throw new ValidationException($"{TestOrderReasonCodes.InvalidOrderSet}: orderedEndpointIds là bắt buộc.");
        }

        var normalizedOrderedIds = orderedEndpointIds
            .Where(x => x != Guid.Empty)
            .ToList();

        if (normalizedOrderedIds.Count != proposedOrder.Count)
        {
            throw new ValidationException($"{TestOrderReasonCodes.InvalidOrderSet}: orderedEndpointIds phải có cùng số lượng với ProposedOrder.");
        }

        if (normalizedOrderedIds.Distinct().Count() != normalizedOrderedIds.Count)
        {
            throw new ValidationException($"{TestOrderReasonCodes.InvalidOrderSet}: orderedEndpointIds không được chứa phần tử trùng.");
        }

        var proposedIds = proposedOrder.Select(x => x.EndpointId).ToHashSet();
        var outOfScopeIds = normalizedOrderedIds.Where(x => !proposedIds.Contains(x)).ToList();
        if (outOfScopeIds.Count > 0)
        {
            throw new ValidationException($"{TestOrderReasonCodes.InvalidOrderSet}: orderedEndpointIds chứa endpoint ngoài phạm vi proposal.");
        }

        var missingIds = proposedIds.Where(x => !normalizedOrderedIds.Contains(x)).ToList();
        if (missingIds.Count > 0)
        {
            throw new ValidationException($"{TestOrderReasonCodes.InvalidOrderSet}: orderedEndpointIds thiếu endpoint trong proposal.");
        }

        return normalizedOrderedIds;
    }

    public IReadOnlyList<ApiOrderItemModel> DeserializeOrderJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<ApiOrderItemModel>();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<ApiOrderItemModel>>(json, DeserializeOptions) ?? new List<ApiOrderItemModel>();
            return items
                .OrderBy(x => x.OrderIndex)
                .ThenBy(x => x.EndpointId)
                .Select((item, index) => new ApiOrderItemModel
                {
                    EndpointId = item.EndpointId,
                    HttpMethod = item.HttpMethod,
                    Path = item.Path,
                    OrderIndex = index + 1,
                    DependsOnEndpointIds = item.DependsOnEndpointIds ?? new List<Guid>(),
                    ReasonCodes = item.ReasonCodes ?? new List<string>(),
                    IsAuthRelated = item.IsAuthRelated,
                })
                .ToList();
        }
        catch (JsonException ex)
        {
            throw new ValidationException("Dữ liệu thứ tự API không hợp lệ.", ex);
        }
    }

    public string SerializeOrderJson(IReadOnlyCollection<ApiOrderItemModel> items)
    {
        if (items == null)
        {
            return null;
        }

        var normalizedItems = items
            .OrderBy(x => x.OrderIndex)
            .ThenBy(x => x.EndpointId)
            .Select((item, index) => new ApiOrderItemModel
            {
                EndpointId = item.EndpointId,
                HttpMethod = item.HttpMethod,
                Path = item.Path,
                OrderIndex = index + 1,
                DependsOnEndpointIds = item.DependsOnEndpointIds ?? new List<Guid>(),
                ReasonCodes = item.ReasonCodes ?? new List<string>(),
                IsAuthRelated = item.IsAuthRelated,
            })
            .ToList();

        return JsonSerializer.Serialize(normalizedItems, SerializeOptions);
    }

    private static int GetMethodWeight(string method)
    {
        if (string.IsNullOrWhiteSpace(method))
        {
            return int.MaxValue;
        }

        return MethodWeights.TryGetValue(method.Trim(), out var weight) ? weight : int.MaxValue;
    }

    private static List<string> BuildReasonCodes(bool isAuthRelated, int dependencyCount)
    {
        var reasonCodes = new List<string>();

        if (isAuthRelated)
        {
            reasonCodes.Add("AUTH_FIRST");
        }

        if (dependencyCount > 0)
        {
            reasonCodes.Add("DEPENDENCY_FIRST");
        }

        reasonCodes.Add("DETERMINISTIC_TIE_BREAK");

        return reasonCodes;
    }
}
