using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class AddTestGenerationJob : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestGenerationJobs",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TriggeredById = table.Column<Guid>(type: "uuid", nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TestCasesGenerated = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    WebhookName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    WebhookUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CallbackUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestGenerationJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestGenerationJobs_TestSuites_TestSuiteId",
                        column: x => x.TestSuiteId,
                        principalSchema: "testgen",
                        principalTable: "TestSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestGenerationJobs_QueuedAt",
                schema: "testgen",
                table: "TestGenerationJobs",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_TestGenerationJobs_Status",
                schema: "testgen",
                table: "TestGenerationJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TestGenerationJobs_TestSuiteId",
                schema: "testgen",
                table: "TestGenerationJobs",
                column: "TestSuiteId");

            migrationBuilder.CreateIndex(
                name: "IX_TestGenerationJobs_TestSuiteId_QueuedAt",
                schema: "testgen",
                table: "TestGenerationJobs",
                columns: new[] { "TestSuiteId", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TestGenerationJobs_TriggeredById",
                schema: "testgen",
                table: "TestGenerationJobs",
                column: "TriggeredById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestGenerationJobs",
                schema: "testgen");
        }
    }
}
