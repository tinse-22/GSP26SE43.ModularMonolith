using ClassifiedAds.Modules.Storage.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ClassifiedAds.Modules.Storage.DbConfigurations;

public class FileEntryConfiguration : IEntityTypeConfiguration<FileEntry>
{
    public void Configure(EntityTypeBuilder<FileEntry> builder)
    {
        builder.ToTable("FileEntries");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");

        // New ERD properties
        builder.Property(x => x.ContentType).HasMaxLength(100);
        builder.Property(x => x.FileCategory).HasDefaultValue(FileCategory.Attachment);

        // Indexes
        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => x.FileCategory);
        builder.HasIndex(x => x.Deleted);

        // Ignore computed properties (aliases)
        builder.Ignore(x => x.FileSize);
        builder.Ignore(x => x.StoragePath);
        builder.Ignore(x => x.IsDeleted);
        builder.Ignore(x => x.DeletedAt);
    }
}

public class DeletedFileEntryConfiguration : IEntityTypeConfiguration<DeletedFileEntry>
{
    public void Configure(EntityTypeBuilder<DeletedFileEntry> builder)
    {
        builder.ToTable("DeletedFileEntries");
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
    }
}
