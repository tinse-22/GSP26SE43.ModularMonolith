using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

/// <summary>
/// Request body for manually creating a test case.
/// </summary>
public class AddTestCaseRequest
{
    /// <summary>
    /// Optional reference to API endpoint being tested.
    /// </summary>
    public Guid? EndpointId { get; set; }

    /// <summary>
    /// Test case name (required, max 200 chars).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Test case description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Test type: HappyPath, Boundary, Negative, Performance, Security.
    /// </summary>
    public TestType TestType { get; set; }

    /// <summary>
    /// Priority: Critical, High, Medium, Low.
    /// </summary>
    public TestPriority Priority { get; set; }

    /// <summary>
    /// Whether this test case is enabled (default true).
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Tags for categorization.
    /// </summary>
    public List<string> Tags { get; set; }

    /// <summary>
    /// Request definition.
    /// </summary>
    public TestCaseRequestInput Request { get; set; }

    /// <summary>
    /// Expectation definition.
    /// </summary>
    public TestCaseExpectationInput Expectation { get; set; }

    /// <summary>
    /// Variable extraction definitions.
    /// </summary>
    public List<TestCaseVariableInput> Variables { get; set; }
}

/// <summary>
/// Request body for updating a test case.
/// </summary>
public class UpdateTestCaseRequest : AddTestCaseRequest
{
}

/// <summary>
/// Request body for toggling test case enabled/disabled.
/// </summary>
public class ToggleTestCaseRequest
{
    public bool IsEnabled { get; set; }
}

/// <summary>
/// Request body for reordering test cases.
/// </summary>
public class ReorderTestCasesRequest
{
    /// <summary>
    /// Ordered list of test case IDs defining the new order.
    /// </summary>
    public List<Guid> TestCaseIds { get; set; }
}

/// <summary>
/// Input model for test case request definition.
/// </summary>
public class TestCaseRequestInput
{
    public HttpMethod HttpMethod { get; set; }
    public string Url { get; set; }
    public string Headers { get; set; }
    public string PathParams { get; set; }
    public string QueryParams { get; set; }
    public BodyType BodyType { get; set; }
    public string Body { get; set; }
    public int Timeout { get; set; } = 30000;
}

/// <summary>
/// Input model for test case expectation definition.
/// </summary>
public class TestCaseExpectationInput
{
    public string ExpectedStatus { get; set; }
    public string ResponseSchema { get; set; }
    public string HeaderChecks { get; set; }
    public string BodyContains { get; set; }
    public string BodyNotContains { get; set; }
    public string JsonPathChecks { get; set; }
    public int? MaxResponseTime { get; set; }
}

/// <summary>
/// Input model for test case variable definition.
/// </summary>
public class TestCaseVariableInput
{
    public string VariableName { get; set; }
    public ExtractFrom ExtractFrom { get; set; }
    public string JsonPath { get; set; }
    public string HeaderName { get; set; }
    public string Regex { get; set; }
    public string DefaultValue { get; set; }
}
