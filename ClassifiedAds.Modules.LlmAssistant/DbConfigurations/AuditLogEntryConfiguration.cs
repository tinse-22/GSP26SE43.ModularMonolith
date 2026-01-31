using ClassifiedAds.Modules.LlmAssistant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.LlmAssistant.DbConfigurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("AuditLogEntries");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
    }
}
