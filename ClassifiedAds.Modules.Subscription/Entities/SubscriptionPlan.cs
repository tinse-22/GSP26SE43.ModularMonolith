using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Subscription.Entities;

/// <summary>
/// Subscription plan definition (Free, Pro, Enterprise).
/// </summary>
public class SubscriptionPlan : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Internal plan name (e.g., "Free", "Pro", "Enterprise").
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Display name for UI.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Plan description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Monthly price in the specified currency.
    /// </summary>
    public decimal? PriceMonthly { get; set; }

    /// <summary>
    /// Yearly price in the specified currency.
    /// </summary>
    public decimal? PriceYearly { get; set; }

    /// <summary>
    /// Currency code (e.g., "USD", "VND").
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Whether this plan is currently available.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Display order in pricing page.
    /// </summary>
    public int SortOrder { get; set; }
}
