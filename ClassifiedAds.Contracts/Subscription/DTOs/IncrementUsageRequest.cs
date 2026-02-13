using ClassifiedAds.Contracts.Subscription.Enums;
using System;

namespace ClassifiedAds.Contracts.Subscription.DTOs;

public class IncrementUsageRequest
{
    public Guid UserId { get; set; }

    public LimitType LimitType { get; set; }

    /// <summary>
    /// The amount to increment. For ProjectCount, typically 1.
    /// For StorageUsedMB, the file size in MB.
    /// </summary>
    public decimal IncrementValue { get; set; }
}
