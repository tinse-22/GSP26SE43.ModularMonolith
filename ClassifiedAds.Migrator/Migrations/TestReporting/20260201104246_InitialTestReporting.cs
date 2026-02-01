using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestReporting
{
    /// <inheritdoc />
    public partial class InitialTestReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "testreporting");

            migrationBuilder.CreateTable(
                name: "ArchivedOutboxMessages",
                schema: "testreporting",
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
                schema: "testreporting",
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
                name: "CoverageMetrics",
                schema: "testreporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TotalEndpoints = table.Column<int>(type: "integer", nullable: false),
                    TestedEndpoints = table.Column<int>(type: "integer", nullable: false),
                    CoveragePercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    ByMethod = table.Column<string>(type: "jsonb", nullable: true),
                    ByTag = table.Column<string>(type: "jsonb", nullable: true),
                    UncoveredPaths = table.Column<string>(type: "jsonb", nullable: true),
                    CalculatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CoverageMetrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "testreporting",
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

            migrationBuilder.CreateTable(
                name: "TestReports",
                schema: "testreporting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedById = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReportType = table.Column<int>(type: "integer", nullable: false),
                    Format = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedOutboxMessages_CreatedDateTime",
                schema: "testreporting",
                table: "ArchivedOutboxMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_CoverageMetrics_TestRunId",
                schema: "testreporting",
                table: "CoverageMetrics",
                column: "TestRunId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CreatedDateTime",
                schema: "testreporting",
                table: "OutboxMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Published_CreatedDateTime",
                schema: "testreporting",
                table: "OutboxMessages",
                columns: new[] { "Published", "CreatedDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_TestReports_FileId",
                schema: "testreporting",
                table: "TestReports",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_TestReports_GeneratedById",
                schema: "testreporting",
                table: "TestReports",
                column: "GeneratedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestReports_TestRunId",
                schema: "testreporting",
                table: "TestReports",
                column: "TestRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArchivedOutboxMessages",
                schema: "testreporting");

            migrationBuilder.DropTable(
                name: "AuditLogEntries",
                schema: "testreporting");

            migrationBuilder.DropTable(
                name: "CoverageMetrics",
                schema: "testreporting");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "testreporting");

            migrationBuilder.DropTable(
                name: "TestReports",
                schema: "testreporting");
        }
    }
}
