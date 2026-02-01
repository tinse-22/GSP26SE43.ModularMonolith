using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Storage
{
    /// <inheritdoc />
    public partial class InitialStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "storage");

            migrationBuilder.CreateTable(
                name: "ArchivedOutboxMessages",
                schema: "storage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: true),
                    TriggeredById = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectId = table.Column<string>(type: "text", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    ActivityId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                schema: "storage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: true),
                    ObjectId = table.Column<string>(type: "text", nullable: true),
                    Log = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeletedFileEntries",
                schema: "storage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FileEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeletedFileEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FileEntries",
                schema: "storage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    UploadedTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: true),
                    FileLocation = table.Column<string>(type: "text", nullable: true),
                    Encrypted = table.Column<bool>(type: "boolean", nullable: false),
                    EncryptionKey = table.Column<string>(type: "text", nullable: true),
                    EncryptionIV = table.Column<string>(type: "text", nullable: true),
                    Archived = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    FileCategory = table.Column<int>(type: "integer", nullable: false, defaultValue: 3),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "storage",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: true),
                    TriggeredById = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectId = table.Column<string>(type: "text", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    ActivityId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedOutboxMessages_CreatedDateTime",
                schema: "storage",
                table: "ArchivedOutboxMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_FileEntries_Deleted",
                schema: "storage",
                table: "FileEntries",
                column: "Deleted");

            migrationBuilder.CreateIndex(
                name: "IX_FileEntries_FileCategory",
                schema: "storage",
                table: "FileEntries",
                column: "FileCategory");

            migrationBuilder.CreateIndex(
                name: "IX_FileEntries_OwnerId",
                schema: "storage",
                table: "FileEntries",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CreatedDateTime",
                schema: "storage",
                table: "OutboxMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Published_CreatedDateTime",
                schema: "storage",
                table: "OutboxMessages",
                columns: new[] { "Published", "CreatedDateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivedOutboxMessages",
                schema: "storage");

            migrationBuilder.DropTable(
                name: "AuditLogEntries",
                schema: "storage");

            migrationBuilder.DropTable(
                name: "DeletedFileEntries",
                schema: "storage");

            migrationBuilder.DropTable(
                name: "FileEntries",
                schema: "storage");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "storage");
        }
    }
}
