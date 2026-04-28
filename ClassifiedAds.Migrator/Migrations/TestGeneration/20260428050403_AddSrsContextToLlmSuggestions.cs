using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class AddSrsContextToLlmSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SrsDocumentId",
                schema: "testgen",
                table: "LlmSuggestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CoveredRequirementIds",
                schema: "testgen",
                table: "LlmSuggestions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SrsDocumentId",
                schema: "testgen",
                table: "LlmSuggestions");

            migrationBuilder.DropColumn(
                name: "CoveredRequirementIds",
                schema: "testgen",
                table: "LlmSuggestions");
        }
    }
}
