# Database Migration Plan: Multiple Databases → Single Database with Schema Isolation

## Overview

This document describes the migration from **6 separate PostgreSQL databases** (database-per-module) to a **single PostgreSQL database** with **schema-per-module** isolation.

## Architecture Change Summary

### Before (Current State)
```
┌─────────────────────────────────────────────────────────────────────┐
│                     6 Separate Databases                            │
├─────────────────────────────────────────────────────────────────────┤
│  ClassifiedAds_Product     │ ClassifiedAds_Identity                 │
│  ClassifiedAds_Storage     │ ClassifiedAds_Notification             │
│  ClassifiedAds_AuditLog    │ ClassifiedAds_Configuration            │
└─────────────────────────────────────────────────────────────────────┘
```

### After (Target State)
```
┌─────────────────────────────────────────────────────────────────────┐
│                     Single Database: ClassifiedAds                  │
├─────────────────────────────────────────────────────────────────────┤
│  Schema: product        │ Schema: identity                          │
│  Schema: storage        │ Schema: notification                      │
│  Schema: auditlog       │ Schema: configuration                     │
└─────────────────────────────────────────────────────────────────────┘
```

## Schema Mapping

| Module         | Old Database               | New Schema      |
|----------------|----------------------------|-----------------|
| Product        | ClassifiedAds_Product      | `product`       |
| Identity       | ClassifiedAds_Identity     | `identity`      |
| Storage        | ClassifiedAds_Storage      | `storage`       |
| Notification   | ClassifiedAds_Notification | `notification`  |
| AuditLog       | ClassifiedAds_AuditLog     | `auditlog`      |
| Configuration  | ClassifiedAds_Configuration| `configuration` |

## Code Changes Made

### 1. DbContext Changes
Each DbContext now sets a default schema in `OnModelCreating`:

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);
    builder.HasDefaultSchema("product"); // Schema name matches module
    builder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
}
```

**Files Modified:**
- `ClassifiedAds.Modules.Product/Persistence/ProductDbContext.cs`
- `ClassifiedAds.Modules.Identity/Persistence/IdentityDbContext.cs`
- `ClassifiedAds.Modules.Storage/Persistence/StorageDbContext.cs`
- `ClassifiedAds.Modules.Notification/Persistence/NotificationDbContext.cs`
- `ClassifiedAds.Modules.AuditLog/Persistence/AuditLogDbContext.cs`
- `ClassifiedAds.Modules.Configuration/Persistence/ConfigurationDbContext.cs`

### 2. Configuration Changes

**Shared Connection String Pattern:**
```json
{
  "ConnectionStrings": {
    "Default": "Host=127.0.0.1;Port=5432;Database=ClassifiedAds;Username=postgres;Password=<YOUR_PASSWORD>"
  }
}
```

**Files Modified:**
- `ClassifiedAds.WebAPI/appsettings.json`
- `ClassifiedAds.Background/appsettings.json`
- `ClassifiedAds.Migrator/appsettings.json`
- `.env`
- `docker-compose.yml`

### 3. Module Registration Changes

Each module is now registered with the shared connection string:

```csharp
var sharedConnectionString = configuration.GetConnectionString("Default");

services.AddProductModule(opt =>
{
    configuration.GetSection("Modules:Product").Bind(opt);
    opt.ConnectionStrings ??= new ConnectionStringsOptions();
    opt.ConnectionStrings.Default = sharedConnectionString;
});
```

**Files Modified:**
- `ClassifiedAds.WebAPI/Program.cs`
- `ClassifiedAds.Background/Program.cs`
- `ClassifiedAds.Migrator/Program.cs`

## Migration Steps for Fresh Deployment

For a **new environment** with no existing data:

1. **Ensure PostgreSQL is running**
   ```powershell
   docker-compose up -d db
   ```

2. **Run Migrations**
   ```powershell
   docker-compose up migrator
   # OR locally:
   dotnet run --project ClassifiedAds.Migrator
   ```

3. **Verify Schemas Created**
   ```sql
   SELECT schema_name FROM information_schema.schemata 
   WHERE schema_name IN ('product', 'identity', 'storage', 'notification', 'auditlog', 'configuration');
   ```

4. **Start Application**
   ```powershell
   docker-compose up -d webapi background
   ```

## Migration Steps for Existing Data

For environments with **existing data** in the old databases:

### Step 1: Backup All Databases
```powershell
pg_dump -h localhost -U postgres -d ClassifiedAds_Product > backup_product.sql
pg_dump -h localhost -U postgres -d ClassifiedAds_Identity > backup_identity.sql
pg_dump -h localhost -U postgres -d ClassifiedAds_Storage > backup_storage.sql
pg_dump -h localhost -U postgres -d ClassifiedAds_Notification > backup_notification.sql
pg_dump -h localhost -U postgres -d ClassifiedAds_AuditLog > backup_auditlog.sql
pg_dump -h localhost -U postgres -d ClassifiedAds_Configuration > backup_configuration.sql
```

### Step 2: Create New Database and Schemas
```sql
CREATE DATABASE "ClassifiedAds";

\c ClassifiedAds

CREATE SCHEMA IF NOT EXISTS product;
CREATE SCHEMA IF NOT EXISTS identity;
CREATE SCHEMA IF NOT EXISTS storage;
CREATE SCHEMA IF NOT EXISTS notification;
CREATE SCHEMA IF NOT EXISTS auditlog;
CREATE SCHEMA IF NOT EXISTS configuration;
```

### Step 3: Run EF Core Migrations
```powershell
dotnet run --project ClassifiedAds.Migrator
```

### Step 4: Migrate Data (Per Module)

For each module, export data from the old database and import to the new schema:

```sql
-- Example for Product module
-- Export from old database
\c ClassifiedAds_Product
\copy "Products" TO '/tmp/products.csv' CSV HEADER;
\copy "OutboxMessages" TO '/tmp/product_outbox.csv' CSV HEADER;
\copy "ArchivedOutboxMessages" TO '/tmp/product_archived_outbox.csv' CSV HEADER;
\copy "AuditLogEntries" TO '/tmp/product_audit.csv' CSV HEADER;

-- Import to new schema
\c ClassifiedAds
\copy product."Products" FROM '/tmp/products.csv' CSV HEADER;
\copy product."OutboxMessages" FROM '/tmp/product_outbox.csv' CSV HEADER;
\copy product."ArchivedOutboxMessages" FROM '/tmp/product_archived_outbox.csv' CSV HEADER;
\copy product."AuditLogEntries" FROM '/tmp/product_audit.csv' CSV HEADER;
```

**Repeat for all modules with appropriate table names.**

### Step 5: Verify Migration
```sql
-- Check row counts match between old and new
SELECT COUNT(*) FROM ClassifiedAds_Product."Products";
SELECT COUNT(*) FROM ClassifiedAds.product."Products";
```

### Step 6: Update Application Configuration
Deploy the updated code with single connection string configuration.

### Step 7: Test Application
- Verify API endpoints work
- Verify data is accessible
- Verify audit logging works
- Verify notifications send correctly

### Step 8: Decommission Old Databases
After successful verification:
```sql
DROP DATABASE ClassifiedAds_Product;
DROP DATABASE ClassifiedAds_Identity;
DROP DATABASE ClassifiedAds_Storage;
DROP DATABASE ClassifiedAds_Notification;
DROP DATABASE ClassifiedAds_AuditLog;
DROP DATABASE ClassifiedAds_Configuration;
```

## Rollback Plan

If migration fails:

1. **Stop Application**
   ```powershell
   docker-compose down webapi background
   ```

2. **Restore Old Configuration**
   - Revert code changes
   - Restore old appsettings.json files

3. **Restore Databases (if needed)**
   ```powershell
   psql -h localhost -U postgres -d ClassifiedAds_Product < backup_product.sql
   # Repeat for other databases
   ```

4. **Restart with Old Configuration**

## Verification Checklist

After migration, verify:

- [ ] All 6 schemas exist in the single database
- [ ] EF Core migrations ran successfully for all modules
- [ ] Application starts without errors
- [ ] API endpoints return expected data
- [ ] New records are created in correct schemas
- [ ] Cross-module queries work (if any)
- [ ] Audit logging works correctly
- [ ] Background workers function properly
- [ ] Docker Compose deployment works

## Benefits of Schema-per-Module

1. **Simplified Operations**: Single database to backup, restore, monitor
2. **Reduced Complexity**: Single connection string across all modules
3. **Better Resource Usage**: Single connection pool shared across modules
4. **Easier Cross-Module Queries**: If needed in the future (via qualified names)
5. **Maintained Isolation**: Schemas provide logical separation between modules
6. **Standard Pattern**: Common approach in modular monoliths

## Limitations & Considerations

1. **Schema Locking**: During migrations, schemas may lock each other
2. **Index Naming**: Ensure index names are unique across schemas
3. **Extension Sharing**: PostgreSQL extensions are database-wide, not schema-specific
4. **Backup Granularity**: Can't backup individual modules separately (but can dump specific schemas)

## Files Changed Summary

| File | Change Description |
|------|-------------------|
| `ClassifiedAds.WebAPI/appsettings.json` | Single shared connection string |
| `ClassifiedAds.Background/appsettings.json` | Single shared connection string |
| `ClassifiedAds.Migrator/appsettings.json` | Single shared connection string |
| `.env` | Single environment variable for connection |
| `docker-compose.yml` | Updated service configuration |
| `ClassifiedAds.WebAPI/Program.cs` | Inject shared connection string to modules |
| `ClassifiedAds.Background/Program.cs` | Inject shared connection string to modules |
| `ClassifiedAds.Migrator/Program.cs` | Inject shared connection string to modules |
| `**/Persistence/*DbContext.cs` (6 files) | Added `HasDefaultSchema()` |
