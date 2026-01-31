using ClassifiedAds.Modules.TestExecution.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestExecution.DbConfigurations;

public class ExecutionEnvironmentConfiguration : IEntityTypeConfiguration<ExecutionEnvironment>
{
    public void Configure(EntityTypeBuilder<ExecutionEnvironment> builder)
    {
        builder.ToTable("ExecutionEnvironments");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.BaseUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Variables).HasColumnType("jsonb");
        builder.Property(x => x.Headers).HasColumnType("jsonb");
        builder.Property(x => x.AuthConfig).HasColumnType("jsonb");

        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => new { x.ProjectId, x.IsDefault });
    }
}
