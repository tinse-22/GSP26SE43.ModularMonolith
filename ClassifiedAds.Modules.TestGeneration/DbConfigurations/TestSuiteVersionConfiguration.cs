using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestSuiteVersionConfiguration : IEntityTypeConfiguration<Entities.TestSuiteVersion>
{
    public void Configure(EntityTypeBuilder<Entities.TestSuiteVersion> builder)
    {
        builder.ToTable("TestSuiteVersions");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.VersionNumber)
            .IsRequired();

        builder.Property(x => x.ChangeType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ChangeDescription)
            .HasColumnType("text");

        builder.Property(x => x.TestCaseOrderSnapshot)
            .HasColumnType("jsonb");

        builder.Property(x => x.ApprovalStatusSnapshot)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.PreviousState)
            .HasColumnType("jsonb");

        builder.Property(x => x.NewState)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.TestSuiteId);
        builder.HasIndex(x => x.ChangedById);
        builder.HasIndex(x => x.ChangeType);
        builder.HasIndex(x => new { x.TestSuiteId, x.VersionNumber });

        builder.HasOne(x => x.TestSuite)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.TestSuiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
