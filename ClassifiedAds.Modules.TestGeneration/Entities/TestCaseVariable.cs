using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestGeneration.Entities;

/// <summary>
/// Variable extraction from test response for chaining.
/// </summary>
public class TestCaseVariable : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Test case this variable belongs to.
    /// </summary>
    public Guid TestCaseId { get; set; }

    /// <summary>
    /// Variable name for reference in subsequent tests.
    /// </summary>
    public string VariableName { get; set; }

    /// <summary>
    /// Where to extract from: ResponseBody, ResponseHeader, Status.
    /// </summary>
    public ExtractFrom ExtractFrom { get; set; }

    /// <summary>
    /// JSONPath expression for body extraction.
    /// </summary>
    public string JsonPath { get; set; }

    /// <summary>
    /// Header name for header extraction.
    /// </summary>
    public string HeaderName { get; set; }

    /// <summary>
    /// Regex pattern for extraction.
    /// </summary>
    public string Regex { get; set; }

    /// <summary>
    /// Default value if extraction fails.
    /// </summary>
    public string DefaultValue { get; set; }

    // Navigation properties
    public TestCase TestCase { get; set; }
}

public enum ExtractFrom
{
    ResponseBody = 0,
    ResponseHeader = 1,
    Status = 2
}
