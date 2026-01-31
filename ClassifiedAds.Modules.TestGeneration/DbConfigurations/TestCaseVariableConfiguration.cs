using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestCaseVariableConfiguration : IEntityTypeConfiguration<Entities.TestCaseVariable>
{
    public void Configure(EntityTypeBuilder<Entities.TestCaseVariable> builder)
    {
        builder.ToTable("TestCaseVariables");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.VariableName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ExtractFrom)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.JsonPath)
            .HasMaxLength(500);

        builder.Property(x => x.HeaderName)
            .HasMaxLength(100);

        builder.Property(x => x.Regex)
            .HasMaxLength(500);

        builder.Property(x => x.DefaultValue)
            .HasColumnType("text");

        builder.HasIndex(x => x.TestCaseId);

        builder.HasOne(x => x.TestCase)
            .WithMany(x => x.Variables)
            .HasForeignKey(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
