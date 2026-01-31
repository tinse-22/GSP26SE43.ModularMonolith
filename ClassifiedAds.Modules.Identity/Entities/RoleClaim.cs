using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.Identity.Entities;

public class RoleClaim : Entity<Guid>
{
    public Guid RoleId { get; set; }

    public string Type { get; set; }

    public string Value { get; set; }

    public Role Role { get; set; }
}
