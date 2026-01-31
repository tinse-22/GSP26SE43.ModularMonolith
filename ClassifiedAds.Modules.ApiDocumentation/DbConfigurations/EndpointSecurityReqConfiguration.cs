using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.ApiDocumentation.DbConfigurations;

public class EndpointSecurityReqConfiguration : IEntityTypeConfiguration<Entities.EndpointSecurityReq>
{
    public void Configure(EntityTypeBuilder<Entities.EndpointSecurityReq> builder)
    {
        builder.ToTable("EndpointSecurityReqs");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.SecurityType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.SchemeName)
            .HasMaxLength(100);

        builder.Property(x => x.Scopes)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.EndpointId);

        builder.HasOne(x => x.Endpoint)
            .WithMany(x => x.SecurityRequirements)
            .HasForeignKey(x => x.EndpointId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
