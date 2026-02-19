using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public class TestSuiteScopeService : ITestSuiteScopeService
{
    public List<Guid> NormalizeEndpointIds(IReadOnlyCollection<Guid> endpointIds)
    {
        if (endpointIds == null || endpointIds.Count == 0)
        {
            return new List<Guid>();
        }

        return endpointIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }
}
