using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class TestOrderProposalConfiguration : IEntityTypeConfiguration<Entities.TestOrderProposal>
{
    public void Configure(EntityTypeBuilder<Entities.TestOrderProposal> builder)
    {
        builder.ToTable("TestOrderProposals");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.ProposalNumber)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .HasColumnType("bytea")
            .IsConcurrencyToken()
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.Source)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(x => x.ProposedOrder)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.AiReasoning)
            .HasColumnType("text");

        builder.Property(x => x.ConsideredFactors)
            .HasColumnType("jsonb");

        builder.Property(x => x.ReviewNotes)
            .HasColumnType("text");

        builder.Property(x => x.UserModifiedOrder)
            .HasColumnType("jsonb");

        builder.Property(x => x.AppliedOrder)
            .HasColumnType("jsonb");

        builder.Property(x => x.LlmModel)
            .HasMaxLength(100);

        builder.HasIndex(x => x.TestSuiteId);
        builder.HasIndex(x => x.ReviewedById);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.Source);
        builder.HasIndex(x => new { x.TestSuiteId, x.ProposalNumber });

        builder.HasOne(x => x.TestSuite)
            .WithMany(x => x.OrderProposals)
            .HasForeignKey(x => x.TestSuiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
