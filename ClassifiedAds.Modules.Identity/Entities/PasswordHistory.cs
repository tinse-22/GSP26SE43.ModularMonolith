using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Identity.Entities;

/// <summary>
/// Stores hashed passwords to prevent password reuse.
/// Used by HistoricalPasswordValidator to enforce password history policy.
/// </summary>
public class PasswordHistory : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// The user this password history entry belongs to.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Navigation property to the user.
    /// </summary>
    public User User { get; set; }

    /// <summary>
    /// The hashed password (using same algorithm as current password).
    /// </summary>
    public string PasswordHash { get; set; }
}
