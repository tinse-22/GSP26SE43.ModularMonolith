using ClassifiedAds.Modules.Storage.Entities;
using ClassifiedAds.Persistence.PostgreSQL;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Storage.Persistence;

public class StorageDbContext : DbContextUnitOfWork<StorageDbContext>
{
    public const string DefaultSchema = "classifiedads_storage";

    public StorageDbContext(DbContextOptions<StorageDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema(DefaultSchema);
        builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override int SaveChanges()
    {
        SetOutboxActivityId();
        HandleFileEntriesDeleted();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetOutboxActivityId();
        HandleFileEntriesDeleted();
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is ObjectDisposedException ode
            && ode.Message.Contains("ManualResetEventSlim"))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[TRANSIENT-NPGSQL] {GetType().Name}.SaveChangesAsync failed: {ode.Message}");
            throw;
        }
    }

    private void SetOutboxActivityId()
    {
        var entities = ChangeTracker.Entries<OutboxMessage>();
        foreach (var entity in entities.Where(e => e.State == EntityState.Added))
        {
            var outbox = entity.Entity;

            if (string.IsNullOrWhiteSpace(outbox.ActivityId))
            {
                outbox.ActivityId = System.Diagnostics.Activity.Current?.Id;
            }
        }
    }

    private void HandleFileEntriesDeleted()
    {
        var entities = ChangeTracker.Entries<FileEntry>();
        foreach (var entity in entities.Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var fileEntry = entity.Entity;

            if (fileEntry.Deleted)
            {
                Set<DeletedFileEntry>().Add(new DeletedFileEntry
                {
                    FileEntryId = fileEntry.Id,
                    CreatedDateTime = fileEntry.DeletedDate ?? System.DateTimeOffset.Now
                });
            }
        }
    }
}
