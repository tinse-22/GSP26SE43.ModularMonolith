using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
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
    private readonly IApiTestOrderAlgorithm _apiTestOrderAlgorithm;

    public ApiTestOrderService(
        IApiEndpointMetadataService endpointMetadataService,
        IApiTestOrderAlgorithm apiTestOrderAlgorithm)
    {
        _endpointMetadataService = endpointMetadataService;
        _apiTestOrderAlgorithm = apiTestOrderAlgorithm;
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
            throw new ValidationException("Không tìm thấy metadata endpoint để xây dựng đề xuất thứ tự API.");
        }

        return _apiTestOrderAlgorithm.BuildProposalOrder(endpoints);
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
}
