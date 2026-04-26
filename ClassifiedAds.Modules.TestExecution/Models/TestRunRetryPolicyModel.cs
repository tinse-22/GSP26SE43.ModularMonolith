using System.Text.Json.Serialization;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class TestRunRetryPolicyModel
{
    public int MaxRetryAttempts { get; set; } = 0;

    /// <summary>
    /// Enables the retry mechanism for failed test cases whose only failures are expectation
    /// mismatches (STATUS_CODE_MISMATCH, RESPONSE_SCHEMA_MISMATCH).  When false, no retries
    /// are attempted regardless of MaxRetryAttempts.
    /// </summary>
    /// <remarks>JSON key kept as "retryFailedDependencies" for backward compatibility.</remarks>
    [JsonPropertyName("retryFailedDependencies")]
    public bool EnableRetry { get; set; } = true;

    public bool RerunSkippedCases { get; set; } = true;

    public override string ToString()
    {
        return $"MaxRetryAttempts={MaxRetryAttempts};EnableRetry={EnableRetry};RerunSkippedCases={RerunSkippedCases}";
    }

    public TestRunRetryPolicyModel Clone()
    {
        return new TestRunRetryPolicyModel
        {
            MaxRetryAttempts = MaxRetryAttempts,
            EnableRetry = EnableRetry,
            RerunSkippedCases = RerunSkippedCases,
        };
    }
}
