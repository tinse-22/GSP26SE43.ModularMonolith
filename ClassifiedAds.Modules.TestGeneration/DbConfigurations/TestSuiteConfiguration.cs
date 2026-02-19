using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestSuiteConfiguration : IEntityTypeConfiguration<Entities.TestSuite>
{
    public void Configure(EntityTypeBuilder<Entities.TestSuite> builder)
    {
        builder.ToTable("TestSuites");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.GenerationType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ApprovalStatus)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasDefaultValue(1);

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.ApiSpecId);

        builder.Property(x => x.SelectedEndpointIds)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.CreatedById);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ApprovalStatus);
        builder.HasIndex(x => x.ApprovedById);
        builder.HasIndex(x => x.LastModifiedById);
    }
}
