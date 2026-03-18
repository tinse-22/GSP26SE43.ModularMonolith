using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Contracts.ApiDocumentation.Services;

/// <summary>
/// Cross-module contract for retrieving structured parameter details from API endpoints.
/// Used by BodyMutationEngine (FE-06) to generate rule-based body mutations.
/// </summary>
public interface IApiEndpointParameterDetailService
{
    /// <summary>
    /// Get structured parameter details for the given endpoints.
    /// </summary>
    /// <param name="specificationId">API specification containing the endpoints.</param>
    /// <param name="endpointIds">Endpoints to retrieve parameters for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Parameter details grouped by endpoint.</returns>
    Task<IReadOnlyList<EndpointParameterDetailDto>> GetParameterDetailsAsync(
        Guid specificationId,
        IReadOnlyCollection<Guid> endpointIds,
        CancellationToken cancellationToken = default);
}
