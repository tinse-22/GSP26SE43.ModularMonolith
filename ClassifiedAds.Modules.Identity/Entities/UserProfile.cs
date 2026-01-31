using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Identity.Entities;

/// <summary>
/// Custom user profile extension for additional user information.
/// Has 1:1 relationship with User.
/// </summary>
public class UserProfile : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// User this profile belongs to (1:1 relationship).
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Display name shown in UI.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// URL to user's avatar image.
    /// </summary>
    public string AvatarUrl { get; set; }

    /// <summary>
    /// User's preferred timezone (e.g., "Asia/Ho_Chi_Minh").
    /// </summary>
    public string Timezone { get; set; }

    // Navigation property
    public User User { get; set; }
}
