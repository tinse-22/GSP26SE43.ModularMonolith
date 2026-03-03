using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.LlmAssistant
{
    /// <inheritdoc />
    public partial class InitialLlmAssistant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "llmassistant");

            migrationBuilder.CreateTable(
                name: "ArchivedOutboxMessages",
                schema: "llmassistant",
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
                schema: "llmassistant",
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
                name: "LlmInteractions",
                schema: "llmassistant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    InteractionType = table.Column<int>(type: "integer", nullable: false),
                    InputContext = table.Column<string>(type: "text", nullable: true),
                    LlmResponse = table.Column<string>(type: "text", nullable: true),
                    ModelUsed = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TokensUsed = table.Column<int>(type: "integer", nullable: false),
                    LatencyMs = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmInteractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LlmSuggestionCaches",
                schema: "llmassistant",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    SuggestionType = table.Column<int>(type: "integer", nullable: false),
                    CacheKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Suggestions = table.Column<string>(type: "jsonb", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmSuggestionCaches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "llmassistant",
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
                schema: "llmassistant",
                table: "ArchivedOutboxMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_LlmInteractions_CreatedDateTime",
                schema: "llmassistant",
                table: "LlmInteractions",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_LlmInteractions_InteractionType",
                schema: "llmassistant",
                table: "LlmInteractions",
                column: "InteractionType");

            migrationBuilder.CreateIndex(
                name: "IX_LlmInteractions_UserId",
                schema: "llmassistant",
                table: "LlmInteractions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestionCaches_CacheKey",
                schema: "llmassistant",
                table: "LlmSuggestionCaches",
                column: "CacheKey");

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestionCaches_EndpointId",
                schema: "llmassistant",
                table: "LlmSuggestionCaches",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestionCaches_ExpiresAt",
                schema: "llmassistant",
                table: "LlmSuggestionCaches",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CreatedDateTime",
                schema: "llmassistant",
                table: "OutboxMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Published_CreatedDateTime",
                schema: "llmassistant",
                table: "OutboxMessages",
                columns: new[] { "Published", "CreatedDateTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivedOutboxMessages",
                schema: "llmassistant");

            migrationBuilder.DropTable(
                name: "AuditLogEntries",
                schema: "llmassistant");

            migrationBuilder.DropTable(
                name: "LlmInteractions",
                schema: "llmassistant");

            migrationBuilder.DropTable(
                name: "LlmSuggestionCaches",
                schema: "llmassistant");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "llmassistant");
        }
    }
}
