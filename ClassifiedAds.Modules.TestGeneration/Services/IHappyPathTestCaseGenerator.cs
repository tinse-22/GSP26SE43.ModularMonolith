using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Core service for generating happy-path test cases from an approved API order.
/// Orchestrates: endpoint metadata → prompt building → n8n/LLM call → entity creation.
/// </summary>
public interface IHappyPathTestCaseGenerator
{
    /// <summary>
    /// Generates happy-path test cases for a test suite using LLM-assisted generation via n8n.
    /// </summary>
    /// <param name="suite">The test suite (must have an approved order).</param>
    /// <param name="orderedEndpoints">Approved API order items.</param>
    /// <param name="specificationId">API specification ID for fetching metadata.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Generated test case entities ready for persistence.</returns>
    Task<HappyPathGenerationResult> GenerateAsync(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        Guid specificationId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from happy-path test case generation.
/// </summary>
public class HappyPathGenerationResult
{
    public IReadOnlyList<TestCase> TestCases { get; set; } = Array.Empty<TestCase>();
    public string LlmModel { get; set; }
    public int? TokensUsed { get; set; }
    public string Reasoning { get; set; }
    public int EndpointsCovered { get; set; }
}
