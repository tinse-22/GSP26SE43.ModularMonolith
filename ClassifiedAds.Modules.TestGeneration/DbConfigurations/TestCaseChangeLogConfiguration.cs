using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestCaseChangeLogConfiguration : IEntityTypeConfiguration<Entities.TestCaseChangeLog>
{
    public void Configure(EntityTypeBuilder<Entities.TestCaseChangeLog> builder)
    {
        builder.ToTable("TestCaseChangeLogs");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ChangeType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.FieldName)
            .HasMaxLength(100);

        builder.Property(x => x.OldValue)
            .HasColumnType("text");

        builder.Property(x => x.NewValue)
            .HasColumnType("text");

        builder.Property(x => x.ChangeReason)
            .HasColumnType("text");

        builder.Property(x => x.IpAddress)
            .HasMaxLength(45);

        builder.Property(x => x.UserAgent)
            .HasMaxLength(500);

        builder.HasIndex(x => x.TestCaseId);
        builder.HasIndex(x => x.ChangedById);
        builder.HasIndex(x => x.ChangeType);
        builder.HasIndex(x => x.CreatedDateTime);

        builder.HasOne(x => x.TestCase)
            .WithMany(x => x.ChangeLogs)
            .HasForeignKey(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
