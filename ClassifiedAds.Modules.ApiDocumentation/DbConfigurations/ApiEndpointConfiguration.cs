using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.ApiDocumentation.DbConfigurations;

public class ApiEndpointConfiguration : IEntityTypeConfiguration<Entities.ApiEndpoint>
{
    public void Configure(EntityTypeBuilder<Entities.ApiEndpoint> builder)
    {
        builder.ToTable("ApiEndpoints");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.HttpMethod)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Path)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(x => x.OperationId)
            .HasMaxLength(200);

        builder.Property(x => x.Summary)
            .HasMaxLength(500);

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.Tags)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.ApiSpecId);
        builder.HasIndex(x => new { x.ApiSpecId, x.HttpMethod, x.Path });

        builder.HasOne(x => x.ApiSpecification)
            .WithMany(x => x.Endpoints)
            .HasForeignKey(x => x.ApiSpecId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
