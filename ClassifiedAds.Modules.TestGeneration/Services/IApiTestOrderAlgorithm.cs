using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Models;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public interface IApiTestOrderAlgorithm
{
    IReadOnlyList<ApiOrderItemModel> BuildProposalOrder(IReadOnlyCollection<ApiEndpointMetadataDto> endpoints);
}
