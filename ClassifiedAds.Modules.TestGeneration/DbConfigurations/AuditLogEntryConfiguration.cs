using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.TestGeneration.DbConfigurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<Entities.AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<Entities.AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
    }
}
