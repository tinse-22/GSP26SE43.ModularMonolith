using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.ApiDocumentation.Services;

/// <summary>
/// Thin wrapper exposing PathParameterTemplateService.GenerateMutations() via cross-module contract.
/// </summary>
public class PathParameterMutationGatewayService : IPathParameterMutationGatewayService
{
    private readonly IPathParameterTemplateService _pathParamService;

    public PathParameterMutationGatewayService(IPathParameterTemplateService pathParamService)
    {
        _pathParamService = pathParamService;
    }

    public IReadOnlyList<PathParameterMutationDto> GenerateMutations(
        string parameterName, string dataType, string format, string defaultValue)
    {
        var mutations = _pathParamService.GenerateMutations(parameterName, dataType, format, defaultValue);

        return mutations.Select(m => new PathParameterMutationDto
        {
            MutationType = m.MutationType,
            Label = m.Label,
            Value = m.Value,
            ExpectedStatusCode = m.ExpectedStatusCode,
            Description = m.Description,
        }).ToList();
    }
}
