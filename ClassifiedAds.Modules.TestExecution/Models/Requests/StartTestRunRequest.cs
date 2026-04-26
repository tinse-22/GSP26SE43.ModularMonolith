using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.TestExecution.Models.Requests;

public class StartTestRunRequest
{
    public Guid? EnvironmentId { get; set; }

    public List<Guid> SelectedTestCaseIds { get; set; }

    public bool StrictValidation { get; set; }

    public TestRunRetryPolicyModel RetryPolicy { get; set; }

    [Range(0, 3, ErrorMessage = "MaxRetryAttempts phải trong khoảng 0–3.")]
    public int MaxRetryAttempts { get; set; } = 0;

    /// <summary>
    /// Enables the retry mechanism for failed test cases.  JSON key kept as
    /// "retryFailedDependencies" for backward compatibility with existing clients.
    /// </summary>
    [System.Text.Json.Serialization.JsonPropertyName("retryFailedDependencies")]
    public bool EnableRetry { get; set; } = true;

    public bool RerunSkippedCases { get; set; } = true;
}
