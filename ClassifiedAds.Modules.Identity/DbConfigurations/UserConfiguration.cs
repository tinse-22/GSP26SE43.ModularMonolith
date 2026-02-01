using ClassifiedAds.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.Identity.DbConfigurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.HasMany(x => x.Claims)
            .WithOne(x => x.User)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.UserRoles)
            .WithOne(x => x.User)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed Admin User
        // Password: Admin@123 (hashed with ASP.NET Core Identity)
        builder.HasData(new List<User>
        {
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                UserName = "tinvtse@gmail.com",
                NormalizedUserName = "TINVTSE@GMAIL.COM",
                Email = "tinvtse@gmail.com",
                NormalizedEmail = "TINVTSE@GMAIL.COM",
                EmailConfirmed = true,
                PasswordHash = "AQAAAAIAAYagAAAAEKyT+qK4VcVGnZsJG3BzjQQv7nqXgvXZ7xgP5Wh8Y0vKzH8xz2Xz7qK4VcVGnZsJG3A=", // Admin@123
                SecurityStamp = "VVPCRDAS3MJWQD5CSW2GWPRADBXEZINA",
                ConcurrencyStamp = "c8554266-b401-4519-9aeb-a9283053fc58",
                LockoutEnabled = true,
                TwoFactorEnabled = false,
                PhoneNumberConfirmed = false,
                AccessFailedCount = 0,
            },
        });
    }
}
