using ClassifiedAds.Modules.Identity.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace ClassifiedAds.Modules.Identity.DbConfigurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("UserProfiles");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.DisplayName)
            .HasMaxLength(200);

        builder.Property(x => x.AvatarUrl)
            .HasMaxLength(500);

        builder.Property(x => x.Timezone)
            .HasMaxLength(50);

        // 1:1 relationship with User
        builder.HasOne(x => x.User)
            .WithOne(x => x.Profile)
            .HasForeignKey<UserProfile>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint on UserId (1:1)
        builder.HasIndex(x => x.UserId).IsUnique();

        // Seed Admin user profile
        builder.HasData(
            new UserProfile
            {
                Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                UserId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                DisplayName = "System Administrator",
                Timezone = "Asia/Ho_Chi_Minh",
            }
        );
    }
}
