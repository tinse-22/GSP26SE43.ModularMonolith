using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Orchestrates boundary/negative test case generation by combining:
/// 1. Rule-based path parameter mutations (via IPathParameterMutationGatewayService)
/// 2. Rule-based body mutations (via IBodyMutationEngine)
/// 3. LLM-suggested scenarios (via ILlmScenarioSuggester)
/// </summary>
public interface IBoundaryNegativeTestCaseGenerator
{
    Task<BoundaryNegativeGenerationResult> GenerateAsync(
        TestSuite suite,
        IReadOnlyList<ApiOrderItemModel> orderedEndpoints,
        Guid specificationId,
        BoundaryNegativeOptions options,
        CancellationToken cancellationToken = default);
}

public class BoundaryNegativeOptions
{
    public bool IncludePathMutations { get; set; } = true;

    public bool IncludeBodyMutations { get; set; } = true;

    public bool IncludeLlmSuggestions { get; set; } = true;

    public Guid UserId { get; set; }
}

public class BoundaryNegativeGenerationResult
{
    public IReadOnlyList<TestCase> TestCases { get; set; } = Array.Empty<TestCase>();

    public int PathMutationCount { get; set; }

    public int BodyMutationCount { get; set; }

    public int LlmSuggestionCount { get; set; }

    public string LlmModel { get; set; }

    public int? LlmTokensUsed { get; set; }

    public int EndpointsCovered { get; set; }
}
