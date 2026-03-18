using System;

namespace ClassifiedAds.Modules.TestGeneration.Models.Requests;

/// <summary>
/// Request body for triggering happy-path test case generation.
/// Requires an approved API order to exist for the target test suite.
/// </summary>
public class GenerateHappyPathTestCasesRequest
{
    /// <summary>
    /// API specification ID to fetch endpoint metadata from.
    /// Must match the spec used in the approved proposal.
    /// </summary>
    public Guid SpecificationId { get; set; }

    /// <summary>
    /// If true, re-generates and replaces existing happy-path test cases for this suite.
    /// If false (default), generation is blocked when happy-path cases already exist.
    /// </summary>
    public bool ForceRegenerate { get; set; }
}
