using System;
using System.ComponentModel.DataAnnotations;

namespace ClassifiedAds.Modules.Subscription.Models;

public class UpsertUsageTrackingModel
{
    [Required]
    public DateOnly PeriodStart { get; set; }

    [Required]
    public DateOnly PeriodEnd { get; set; }

    public bool ReplaceValues { get; set; }

    [Range(0, int.MaxValue)]
    public int ProjectCount { get; set; }

    [Range(0, int.MaxValue)]
    public int EndpointCount { get; set; }

    [Range(0, int.MaxValue)]
    public int TestSuiteCount { get; set; }

    [Range(0, int.MaxValue)]
    public int TestCaseCount { get; set; }

    [Range(0, int.MaxValue)]
    public int TestRunCount { get; set; }

    [Range(0, int.MaxValue)]
    public int LlmCallCount { get; set; }

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal StorageUsedMB { get; set; }
}
