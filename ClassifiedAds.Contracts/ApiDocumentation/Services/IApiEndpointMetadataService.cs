using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.ApiDocumentation.Services;

public interface IApiEndpointMetadataService
{
    Task<IReadOnlyList<ApiEndpointMetadataDto>> GetEndpointMetadataAsync(
        Guid specificationId,
        IReadOnlyCollection<Guid> selectedEndpointIds = null,
        CancellationToken cancellationToken = default);
}
