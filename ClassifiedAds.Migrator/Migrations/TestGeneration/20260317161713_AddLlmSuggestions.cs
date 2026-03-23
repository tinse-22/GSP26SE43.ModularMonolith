using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class AddLlmSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlmSuggestions",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: true),
                    CacheKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    SuggestionType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TestType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SuggestedName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SuggestedDescription = table.Column<string>(type: "text", nullable: true),
                    SuggestedRequest = table.Column<string>(type: "jsonb", nullable: true),
                    SuggestedExpectation = table.Column<string>(type: "jsonb", nullable: true),
                    SuggestedVariables = table.Column<string>(type: "jsonb", nullable: true),
                    SuggestedTags = table.Column<string>(type: "jsonb", nullable: true),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "text", nullable: true),
                    ModifiedContent = table.Column<string>(type: "jsonb", nullable: true),
                    AppliedTestCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    LlmModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TokensUsed = table.Column<int>(type: "integer", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LlmSuggestions_TestSuites_TestSuiteId",
                        column: x => x.TestSuiteId,
                        principalSchema: "testgen",
                        principalTable: "TestSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestions_AppliedTestCaseId",
                schema: "testgen",
                table: "LlmSuggestions",
                column: "AppliedTestCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestions_CacheKey",
                schema: "testgen",
                table: "LlmSuggestions",
                column: "CacheKey");

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestions_EndpointId",
                schema: "testgen",
                table: "LlmSuggestions",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestions_ReviewedById",
                schema: "testgen",
                table: "LlmSuggestions",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestions_TestSuiteId_ReviewStatus",
                schema: "testgen",
                table: "LlmSuggestions",
                columns: new[] { "TestSuiteId", "ReviewStatus" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlmSuggestions",
                schema: "testgen");
        }
    }
}
