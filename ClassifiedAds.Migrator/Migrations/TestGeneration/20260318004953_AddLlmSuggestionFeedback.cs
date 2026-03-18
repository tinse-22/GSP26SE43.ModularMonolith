using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class AddLlmSuggestionFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LlmSuggestionFeedbacks",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SuggestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FeedbackSignal = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LlmSuggestionFeedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LlmSuggestionFeedbacks_LlmSuggestions_SuggestionId",
                        column: x => x.SuggestionId,
                        principalSchema: "testgen",
                        principalTable: "LlmSuggestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestionFeedbacks_FeedbackSignal",
                schema: "testgen",
                table: "LlmSuggestionFeedbacks",
                column: "FeedbackSignal");

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestionFeedbacks_SuggestionId_UserId",
                schema: "testgen",
                table: "LlmSuggestionFeedbacks",
                columns: new[] { "SuggestionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestionFeedbacks_TestSuiteId_EndpointId",
                schema: "testgen",
                table: "LlmSuggestionFeedbacks",
                columns: new[] { "TestSuiteId", "EndpointId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LlmSuggestionFeedbacks",
                schema: "testgen");
        }
    }
}
