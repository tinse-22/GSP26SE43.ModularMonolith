using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestExecution
{
    /// <inheritdoc />
    public partial class AddTestCaseResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TestCaseResults",
                schema: "testexecution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    HttpStatusCode = table.Column<int>(type: "integer", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    ResolvedUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RequestHeaders = table.Column<string>(type: "jsonb", nullable: true),
                    ResponseHeaders = table.Column<string>(type: "jsonb", nullable: true),
                    ResponseBodyPreview = table.Column<string>(type: "character varying(65536)", maxLength: 65536, nullable: true),
                    FailureReasons = table.Column<string>(type: "jsonb", nullable: true),
                    ExtractedVariables = table.Column<string>(type: "jsonb", nullable: true),
                    DependencyIds = table.Column<string>(type: "jsonb", nullable: true),
                    SkippedBecauseDependencyIds = table.Column<string>(type: "jsonb", nullable: true),
                    StatusCodeMatched = table.Column<bool>(type: "boolean", nullable: false),
                    SchemaMatched = table.Column<bool>(type: "boolean", nullable: true),
                    HeaderChecksPassed = table.Column<bool>(type: "boolean", nullable: true),
                    BodyContainsPassed = table.Column<bool>(type: "boolean", nullable: true),
                    BodyNotContainsPassed = table.Column<bool>(type: "boolean", nullable: true),
                    JsonPathChecksPassed = table.Column<bool>(type: "boolean", nullable: true),
                    ResponseTimePassed = table.Column<bool>(type: "boolean", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseResults", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseResults_Status",
                schema: "testexecution",
                table: "TestCaseResults",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseResults_TestCaseId",
                schema: "testexecution",
                table: "TestCaseResults",
                column: "TestCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseResults_TestRunId",
                schema: "testexecution",
                table: "TestCaseResults",
                column: "TestRunId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseResults_TestRunId_OrderIndex",
                schema: "testexecution",
                table: "TestCaseResults",
                columns: new[] { "TestRunId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseResults_TestRunId_Status",
                schema: "testexecution",
                table: "TestCaseResults",
                columns: new[] { "TestRunId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestCaseResults",
                schema: "testexecution");
        }
    }
}
