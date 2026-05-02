using System;
using System.Collections.Generic;

namespace ClassifiedAds.Contracts.TestGeneration.DTOs;

public class ExecutionTestCaseDto
{
    public Guid TestCaseId { get; set; }

    public Guid? EndpointId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public string TestType { get; set; }

    public int OrderIndex { get; set; }

    public IReadOnlyList<Guid> DependencyIds { get; set; } = Array.Empty<Guid>();

    public ExecutionTestCaseRequestDto Request { get; set; }

    public ExecutionTestCaseExpectationDto Expectation { get; set; }

    public IReadOnlyList<ExecutionVariableRuleDto> Variables { get; set; } = Array.Empty<ExecutionVariableRuleDto>();
}

public class ExecutionTestCaseRequestDto
{
    public string HttpMethod { get; set; }

    public string Url { get; set; }

    public string Headers { get; set; }

    public string PathParams { get; set; }

    public string QueryParams { get; set; }

    public string BodyType { get; set; }

    public string Body { get; set; }

    public int Timeout { get; set; } = 30000;
}

public class ExecutionTestCaseExpectationDto
{
    public string ExpectedStatus { get; set; }

    public string ResponseSchema { get; set; }

    public string HeaderChecks { get; set; }

    public string BodyContains { get; set; }

    public string BodyNotContains { get; set; }

    public string JsonPathChecks { get; set; }

    public int? MaxResponseTime { get; set; }

    public string ExpectationSource { get; set; }

    public string RequirementCode { get; set; }

    public Guid? PrimaryRequirementId { get; set; }
}

public class ExecutionVariableRuleDto
{
    public string VariableName { get; set; }

    public string ExtractFrom { get; set; }

    public string JsonPath { get; set; }

    public string HeaderName { get; set; }

    public string Regex { get; set; }

    public string DefaultValue { get; set; }
}
