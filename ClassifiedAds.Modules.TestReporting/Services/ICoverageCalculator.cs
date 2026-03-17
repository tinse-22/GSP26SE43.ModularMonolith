using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Modules.TestReporting.Models;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestReporting.Services;

public interface ICoverageCalculator
{
    CoverageMetricModel Calculate(
        TestRunReportContextDto context,
        IReadOnlyCollection<ApiEndpointMetadataDto> scopedEndpointMetadata = null);
}
