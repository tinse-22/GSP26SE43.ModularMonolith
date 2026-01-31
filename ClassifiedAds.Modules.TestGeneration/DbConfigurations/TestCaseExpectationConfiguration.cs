using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestCaseExpectationConfiguration : IEntityTypeConfiguration<Entities.TestCaseExpectation>
{
    public void Configure(EntityTypeBuilder<Entities.TestCaseExpectation> builder)
    {
        builder.ToTable("TestCaseExpectations");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ExpectedStatus)
            .HasColumnType("jsonb");

        builder.Property(x => x.ResponseSchema)
            .HasColumnType("jsonb");

        builder.Property(x => x.HeaderChecks)
            .HasColumnType("jsonb");

        builder.Property(x => x.BodyContains)
            .HasColumnType("jsonb");

        builder.Property(x => x.BodyNotContains)
            .HasColumnType("jsonb");

        builder.Property(x => x.JsonPathChecks)
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.TestCaseId).IsUnique();
    }
}
