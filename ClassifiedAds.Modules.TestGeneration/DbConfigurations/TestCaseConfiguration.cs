using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestCaseConfiguration : IEntityTypeConfiguration<Entities.TestCase>
{
    public void Configure(EntityTypeBuilder<Entities.TestCase> builder)
    {
        builder.ToTable("TestCases");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.TestType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Priority)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Tags)
            .HasColumnType("jsonb");

        builder.Property(x => x.Version)
            .HasDefaultValue(1);

        builder.HasIndex(x => x.TestSuiteId);
        builder.HasIndex(x => x.EndpointId);
        builder.HasIndex(x => x.DependsOnId);
        builder.HasIndex(x => new { x.TestSuiteId, x.OrderIndex });
        builder.HasIndex(x => new { x.TestSuiteId, x.CustomOrderIndex });
        builder.HasIndex(x => x.LastModifiedById);

        builder.HasOne(x => x.TestSuite)
            .WithMany(x => x.TestCases)
            .HasForeignKey(x => x.TestSuiteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.DependsOn)
            .WithMany()
            .HasForeignKey(x => x.DependsOnId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Request)
            .WithOne(x => x.TestCase)
            .HasForeignKey<Entities.TestCaseRequest>(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Expectation)
            .WithOne(x => x.TestCase)
            .HasForeignKey<Entities.TestCaseExpectation>(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
