using System;
using System.Linq;
using ClassifiedAds.Modules.Identity.Authorization;
using ClassifiedAds.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Identity.DbConfigurations;

public class RoleClaimConfiguration : IEntityTypeConfiguration<RoleClaim>
{
    // Admin Role ID (must match RoleConfiguration)
    private static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid UserRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public void Configure(EntityTypeBuilder<RoleClaim> builder)
    {
        builder.ToTable("RoleClaims");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        var adminClaims = BuildRoleClaims(
            AdminRoleId,
            RolePermissionMappings.AdminPermissions,
            "0001");

        var userClaims = BuildRoleClaims(
            UserRoleId,
            RolePermissionMappings.UserPermissions,
            "0002");

        var roleClaims = adminClaims.Concat(userClaims).ToArray();

        builder.HasData(roleClaims);
    }

    private static RoleClaim[] BuildRoleClaims(Guid roleId, string[] permissions, string seedGroup)
    {
        var distinctPermissions = permissions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var roleClaims = new RoleClaim[distinctPermissions.Length];
        for (int i = 0; i < distinctPermissions.Length; i++)
        {
            roleClaims[i] = new RoleClaim
            {
                Id = Guid.Parse($"00000000-0000-0000-{seedGroup}-{(i + 1):D12}"),
                RoleId = roleId,
                Type = "Permission",
                Value = distinctPermissions[i],
            };
        }

        return roleClaims;
    }
}
