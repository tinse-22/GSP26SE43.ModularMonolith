using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.ApiDocumentation.DbConfigurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Entities.Project>
{
    public void Configure(EntityTypeBuilder<Entities.Project> builder)
    {
        builder.ToTable("Projects");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasColumnType("text");

        builder.Property(x => x.BaseUrl)
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => x.Status);

        // Self-referencing relationship for ActiveSpec
        builder.HasOne(x => x.ActiveSpec)
            .WithMany()
            .HasForeignKey(x => x.ActiveSpecId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
