using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Identity.Entities;

public class UserClaim : Entity<Guid>
{
    public Guid UserId { get; set; }

    public string Type { get; set; }

    public string Value { get; set; }

    public User User { get; set; }
}
