using System;
using ClassifiedAds.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Identity.DbConfigurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("UserRoles");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        // Seed Admin user with Admin role
        builder.HasData(
            new UserRole
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            }
        );
    }
}
