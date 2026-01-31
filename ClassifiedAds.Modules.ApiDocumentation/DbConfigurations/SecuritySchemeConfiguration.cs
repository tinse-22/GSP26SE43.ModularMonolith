using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.ApiDocumentation.DbConfigurations;

public class SecuritySchemeConfiguration : IEntityTypeConfiguration<Entities.SecurityScheme>
{
    public void Configure(EntityTypeBuilder<Entities.SecurityScheme> builder)
    {
        builder.ToTable("SecuritySchemes");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Scheme)
            .HasMaxLength(50);

        builder.Property(x => x.BearerFormat)
            .HasMaxLength(50);

        builder.Property(x => x.In)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.ParameterName)
            .HasMaxLength(100);

        builder.Property(x => x.Configuration)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.ApiSpecId);

        builder.HasOne(x => x.ApiSpecification)
            .WithMany(x => x.SecuritySchemes)
            .HasForeignKey(x => x.ApiSpecId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
