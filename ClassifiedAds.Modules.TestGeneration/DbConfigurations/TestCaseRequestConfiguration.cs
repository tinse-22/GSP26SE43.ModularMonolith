using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestCaseRequestConfiguration : IEntityTypeConfiguration<Entities.TestCaseRequest>
{
    public void Configure(EntityTypeBuilder<Entities.TestCaseRequest> builder)
    {
        builder.ToTable("TestCaseRequests");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.HttpMethod)
            .HasConversion<string>()
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Url)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.Headers)
            .HasColumnType("jsonb");

        builder.Property(x => x.PathParams)
            .HasColumnType("jsonb");

        builder.Property(x => x.QueryParams)
            .HasColumnType("jsonb");

        builder.Property(x => x.BodyType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Body)
            .HasColumnType("text");

        builder.HasIndex(x => x.TestCaseId).IsUnique();
    }
}
