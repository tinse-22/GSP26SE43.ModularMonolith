using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public interface ITestSuiteScopeService
{
    /// <summary>
    /// Normalizes endpoint IDs by removing duplicates and sorting.
    /// </summary>
    List<Guid> NormalizeEndpointIds(IReadOnlyCollection<Guid> endpointIds);
}
