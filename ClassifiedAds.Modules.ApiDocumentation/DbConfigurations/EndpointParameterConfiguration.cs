using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.ApiDocumentation.DbConfigurations;

public class EndpointParameterConfiguration : IEntityTypeConfiguration<Entities.EndpointParameter>
{
    public void Configure(EntityTypeBuilder<Entities.EndpointParameter> builder)
    {
        builder.ToTable("EndpointParameters");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Location)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.DataType)
            .HasMaxLength(50);

        builder.Property(x => x.Format)
            .HasMaxLength(50);

        builder.Property(x => x.DefaultValue)
            .HasColumnType("text");

        builder.Property(x => x.Schema)
            .HasColumnType("jsonb");

        builder.Property(x => x.Examples)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.EndpointId);

        builder.HasOne(x => x.Endpoint)
            .WithMany(x => x.Parameters)
            .HasForeignKey(x => x.EndpointId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
