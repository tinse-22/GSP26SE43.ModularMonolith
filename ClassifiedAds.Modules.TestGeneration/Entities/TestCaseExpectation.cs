using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Expected response definition for a test case (1:1 relationship).
/// </summary>
public class TestCaseExpectation : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Test case this expectation belongs to.
    /// </summary>
    public Guid TestCaseId { get; set; }

    /// <summary>
    /// Expected status codes (stored as JSON array, e.g., [200, 201]).
    /// </summary>
    public string ExpectedStatus { get; set; }

    /// <summary>
    /// Expected response JSON Schema.
    /// </summary>
    public string ResponseSchema { get; set; }

    /// <summary>
    /// Header validation rules as JSON.
    /// </summary>
    public string HeaderChecks { get; set; }

    /// <summary>
    /// Strings that must be present in response body (JSON array).
    /// </summary>
    public string BodyContains { get; set; }

    /// <summary>
    /// Strings that must NOT be present in response body (JSON array).
    /// </summary>
    public string BodyNotContains { get; set; }

    /// <summary>
    /// JSONPath assertions as JSON object.
    /// </summary>
    public string JsonPathChecks { get; set; }

    /// <summary>
    /// Maximum response time in milliseconds.
    /// </summary>
    public int? MaxResponseTime { get; set; }

    // Navigation properties
    public TestCase TestCase { get; set; }
}
