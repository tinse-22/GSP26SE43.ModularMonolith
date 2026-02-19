using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public interface IApiTestOrderGateService
{
    Task<IReadOnlyList<ApiOrderItemModel>> RequireApprovedOrderAsync(
        Guid testSuiteId,
        CancellationToken cancellationToken = default);

    Task<ApiTestOrderGateStatusModel> GetGateStatusAsync(
        Guid testSuiteId,
        CancellationToken cancellationToken = default);
}
