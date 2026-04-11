using ClassifiedAds.Modules.Storage.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Storage
{
    /// <inheritdoc />
    public partial class RenameStorageSchemaForSupabase : Migration
    {
        private static readonly string[] StorageTables =
        [
            "OutboxMessages",
            "FileEntries",
            "DeletedFileEntries",
            "AuditLogEntries",
            "ArchivedOutboxMessages",
        ];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: StorageDbContext.DefaultSchema);

            foreach (var tableName in StorageTables)
            {
                migrationBuilder.Sql($"""
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.tables
                            WHERE table_schema = 'storage'
                                AND table_name = '{tableName}')
                            AND NOT EXISTS (
                                SELECT 1
                                FROM information_schema.tables
                                WHERE table_schema = '{StorageDbContext.DefaultSchema}'
                                    AND table_name = '{tableName}')
                        THEN
                            EXECUTE 'ALTER TABLE storage."{tableName}" SET SCHEMA {StorageDbContext.DefaultSchema}';
                        END IF;
                    END
                    $$;
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var tableName in StorageTables)
            {
                migrationBuilder.Sql($"""
                    DO $$
                    BEGIN
                        IF EXISTS (
                            SELECT 1
                            FROM pg_namespace
                            WHERE nspname = 'storage')
                            AND EXISTS (
                                SELECT 1
                                FROM information_schema.tables
                                WHERE table_schema = '{StorageDbContext.DefaultSchema}'
                                    AND table_name = '{tableName}')
                            AND NOT EXISTS (
                                SELECT 1
                                FROM information_schema.tables
                                WHERE table_schema = 'storage'
                                    AND table_name = '{tableName}')
                        THEN
                            EXECUTE 'ALTER TABLE {StorageDbContext.DefaultSchema}."{tableName}" SET SCHEMA storage';
                        END IF;
                    END
                    $$;
                    """);
            }
        }
    }
}
