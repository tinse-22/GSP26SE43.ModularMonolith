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

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.ApiSpecId);
        builder.HasIndex(x => x.CreatedById);
        builder.HasIndex(x => x.Status);
    }
}
