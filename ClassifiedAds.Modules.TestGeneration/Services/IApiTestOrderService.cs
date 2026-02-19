using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public interface IApiTestOrderService
{
    Task<IReadOnlyList<ApiOrderItemModel>> BuildProposalOrderAsync(
        Guid suiteId,
        Guid specificationId,
        IReadOnlyCollection<Guid> selectedEndpointIds,
        CancellationToken cancellationToken = default);

    IReadOnlyList<Guid> ValidateReorderedEndpointSet(
        IReadOnlyList<ApiOrderItemModel> proposedOrder,
        IReadOnlyCollection<Guid> orderedEndpointIds);

    IReadOnlyList<ApiOrderItemModel> DeserializeOrderJson(string json);

    string SerializeOrderJson(IReadOnlyCollection<ApiOrderItemModel> items);
}
