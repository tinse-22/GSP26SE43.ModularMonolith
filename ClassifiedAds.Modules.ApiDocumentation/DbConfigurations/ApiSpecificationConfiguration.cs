using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.ApiDocumentation.DbConfigurations;

public class ApiSpecificationConfiguration : IEntityTypeConfiguration<Entities.ApiSpecification>
{
    public void Configure(EntityTypeBuilder<Entities.ApiSpecification> builder)
    {
        builder.ToTable("ApiSpecifications");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.SourceType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Version)
            .HasMaxLength(50);

        builder.Property(x => x.ParseStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ParseErrors)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.IsActive);

        builder.HasOne(x => x.Project)
            .WithMany(x => x.ApiSpecifications)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
