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
        builder.HasIndex(x => x.IsDeleted);

        // Composite index for the main listing query:
        // WHERE ProjectId = @p AND IsDeleted = @p ORDER BY CreatedDateTime DESC
        builder.HasIndex(x => new { x.ProjectId, x.IsDeleted, x.CreatedDateTime })
            .HasDatabaseName("IX_ApiSpecifications_ProjectId_IsDeleted_CreatedDateTime")
            .IsDescending(false, false, true);

        // Composite index for deactivation during project deletion:
        // WHERE ProjectId = @p AND IsActive = true
        builder.HasIndex(x => new { x.ProjectId, x.IsActive })
            .HasDatabaseName("IX_ApiSpecifications_ProjectId_IsActive");

        builder.HasOne(x => x.Project)
            .WithMany(x => x.ApiSpecifications)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
