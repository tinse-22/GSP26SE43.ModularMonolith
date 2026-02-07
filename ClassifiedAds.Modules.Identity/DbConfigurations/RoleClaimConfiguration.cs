using System;
using ClassifiedAds.Modules.Identity.Authorization;
using ClassifiedAds.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Identity.DbConfigurations;

public class RoleClaimConfiguration : IEntityTypeConfiguration<RoleClaim>
{
    // Admin Role ID (must match RoleConfiguration)
    private static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public void Configure(EntityTypeBuilder<RoleClaim> builder)
    {
        builder.ToTable("RoleClaims");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        // Seed all permissions for Admin role
        var permissions = new[]
        {
            // Role permissions
            Permissions.GetRoles,
            Permissions.GetRole,
            Permissions.AddRole,
            Permissions.UpdateRole,
            Permissions.DeleteRole,

            // User permissions
            Permissions.GetUsers,
            Permissions.GetUser,
            Permissions.AddUser,
            Permissions.UpdateUser,
            Permissions.SetPassword,
            Permissions.DeleteUser,

            // User management actions
            Permissions.SendResetPasswordEmail,
            Permissions.SendConfirmationEmailAddressEmail,
            Permissions.AssignRole,
            Permissions.RemoveRole,
            Permissions.LockUser,
            Permissions.UnlockUser,
        };

        var roleClaims = new RoleClaim[permissions.Length];
        for (int i = 0; i < permissions.Length; i++)
        {
            roleClaims[i] = new RoleClaim
            {
                Id = Guid.Parse($"00000000-0000-0000-0001-{(i + 1):D12}"),
                RoleId = AdminRoleId,
                Type = "Permission",
                Value = permissions[i],
            };
        }

        builder.HasData(roleClaims);
    }
}
