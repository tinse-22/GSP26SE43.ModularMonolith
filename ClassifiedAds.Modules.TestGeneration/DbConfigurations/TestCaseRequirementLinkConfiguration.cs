using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestCaseRequirementLinkConfiguration : IEntityTypeConfiguration<TestCaseRequirementLink>
{
    public void Configure(EntityTypeBuilder<TestCaseRequirementLink> builder)
    {
        builder.ToTable("TestCaseRequirementLinks");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.MappingRationale)
            .HasColumnType("text");

        // Unique constraint: one test case maps to each requirement at most once
        builder.HasIndex(x => new { x.TestCaseId, x.SrsRequirementId }).IsUnique();
        builder.HasIndex(x => x.SrsRequirementId);

        // FK to TestCase
        builder.HasOne(x => x.TestCase)
            .WithMany(x => x.RequirementLinks)
            .HasForeignKey(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        // FK to SrsRequirement
        builder.HasOne(x => x.SrsRequirement)
            .WithMany(x => x.TestCaseLinks)
            .HasForeignKey(x => x.SrsRequirementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
