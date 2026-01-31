using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestDataSetConfiguration : IEntityTypeConfiguration<Entities.TestDataSet>
{
    public void Configure(EntityTypeBuilder<Entities.TestDataSet> builder)
    {
        builder.ToTable("TestDataSets");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Data)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(x => x.TestCaseId);

        builder.HasOne(x => x.TestCase)
            .WithMany(x => x.DataSets)
            .HasForeignKey(x => x.TestCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
