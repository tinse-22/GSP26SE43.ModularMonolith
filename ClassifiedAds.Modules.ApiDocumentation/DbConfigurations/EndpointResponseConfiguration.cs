using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.ApiDocumentation.DbConfigurations;

public class EndpointResponseConfiguration : IEntityTypeConfiguration<Entities.EndpointResponse>
{
    public void Configure(EntityTypeBuilder<Entities.EndpointResponse> builder)
    {
        builder.ToTable("EndpointResponses");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.StatusCode)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.Schema)
            .HasColumnType("jsonb");

        builder.Property(x => x.Examples)
            .HasColumnType("jsonb");

        builder.Property(x => x.Headers)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.EndpointId);
        builder.HasIndex(x => new { x.EndpointId, x.StatusCode });

        builder.HasOne(x => x.Endpoint)
            .WithMany(x => x.Responses)
            .HasForeignKey(x => x.EndpointId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
