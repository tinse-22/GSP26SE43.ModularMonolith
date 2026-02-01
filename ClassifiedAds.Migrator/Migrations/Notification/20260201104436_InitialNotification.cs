using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Notification
{
    /// <inheritdoc />
    public partial class InitialNotification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notification");

            migrationBuilder.CreateTable(
                name: "ArchivedEmailMessages",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    From = table.Column<string>(type: "text", nullable: true),
                    Tos = table.Column<string>(type: "text", nullable: true),
                    CCs = table.Column<string>(type: "text", nullable: true),
                    BCCs = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiredDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Log = table.Column<string>(type: "text", nullable: true),
                    SentDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CopyFromId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedEmailMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArchivedSmsMessages",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiredDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Log = table.Column<string>(type: "text", nullable: true),
                    SentDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CopyFromId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArchivedSmsMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailMessages",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    From = table.Column<string>(type: "text", nullable: true),
                    Tos = table.Column<string>(type: "text", nullable: true),
                    CCs = table.Column<string>(type: "text", nullable: true),
                    BCCs = table.Column<string>(type: "text", nullable: true),
                    Subject = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "text", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiredDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Log = table.Column<string>(type: "text", nullable: true),
                    SentDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CopyFromId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SmsMessages",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiredDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Log = table.Column<string>(type: "text", nullable: true),
                    SentDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CopyFromId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmsMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmailMessageAttachments",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EmailMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailMessageAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailMessageAttachments_EmailMessages_EmailMessageId",
                        column: x => x.EmailMessageId,
                        principalSchema: "notification",
                        principalTable: "EmailMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedEmailMessages_CreatedDateTime",
                schema: "notification",
                table: "ArchivedEmailMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedSmsMessages_CreatedDateTime",
                schema: "notification",
                table: "ArchivedSmsMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessageAttachments_EmailMessageId",
                schema: "notification",
                table: "EmailMessageAttachments",
                column: "EmailMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_CreatedDateTime",
                schema: "notification",
                table: "EmailMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_SentDateTime",
                schema: "notification",
                table: "EmailMessages",
                column: "SentDateTime")
                .Annotation("Npgsql:IndexInclude", new[] { "ExpiredDateTime", "AttemptCount", "MaxAttemptCount", "NextAttemptDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_CreatedDateTime",
                schema: "notification",
                table: "SmsMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_SentDateTime",
                schema: "notification",
                table: "SmsMessages",
                column: "SentDateTime")
                .Annotation("Npgsql:IndexInclude", new[] { "ExpiredDateTime", "AttemptCount", "MaxAttemptCount", "NextAttemptDateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivedEmailMessages",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "ArchivedSmsMessages",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "EmailMessageAttachments",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "SmsMessages",
                schema: "notification");

            migrationBuilder.DropTable(
                name: "EmailMessages",
                schema: "notification");
        }
    }
}
