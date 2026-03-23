using System;
using System.Collections.Generic;

namespace ClassifiedAds.Contracts.TestGeneration.DTOs;

public class TestSuiteExecutionContextDto
{
    public TestSuiteAccessContextDto Suite { get; set; }

    public IReadOnlyList<ExecutionTestCaseDto> OrderedTestCases { get; set; } = Array.Empty<ExecutionTestCaseDto>();

    public IReadOnlyList<Guid> OrderedEndpointIds { get; set; } = Array.Empty<Guid>();
}
