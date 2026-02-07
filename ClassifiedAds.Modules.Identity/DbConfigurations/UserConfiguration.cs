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
        // Admin: EmailConfirmed = true (no email verification required)
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
                PasswordHash = "AQAAAAIAAYagAAAAEPfBZpUxae9Dzcv3f2lA5qOYSbJhxh5oYiVhS+j9Q7Rppm2ETqZUaEhWsOYisFocEA==", // Admin@123
                SecurityStamp = "VVPCRDAS3MJWQD5CSW2GWPRADBXEZINA",
                ConcurrencyStamp = "c8554266-b401-4519-9aeb-a9283053fc58",
                LockoutEnabled = true,
                TwoFactorEnabled = false,
                PhoneNumberConfirmed = false,
                AccessFailedCount = 0,
            },
            // Seed Regular User
            // Password: User@123 (hashed with ASP.NET Core Identity)
            // User: EmailConfirmed = false (requires email verification)
            new User
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                UserName = "user@example.com",
                NormalizedUserName = "USER@EXAMPLE.COM",
                Email = "user@example.com",
                NormalizedEmail = "USER@EXAMPLE.COM",
                EmailConfirmed = true,
                PasswordHash = "AQAAAAIAAYagAAAAEDlFqrwIpQDVVwXus3MatUkO1o3wq0iBqGqnXu5DkliD+ic2jmEAvoCCLoonjCzPdA==", // User@123
                SecurityStamp = "XYZPCRDAS3MJWQD5CSW2GWPRADBXEZIN",
                ConcurrencyStamp = "d9665377-c512-5620-0bfc-b0394064gd69",
                LockoutEnabled = true,
                TwoFactorEnabled = false,
                PhoneNumberConfirmed = false,
                AccessFailedCount = 0,
            },
        });
    }
}
