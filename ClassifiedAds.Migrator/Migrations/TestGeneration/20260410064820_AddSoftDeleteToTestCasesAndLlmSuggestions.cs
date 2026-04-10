using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class AddSoftDeleteToTestCasesAndLlmSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "testgen",
                table: "TestCases",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "testgen",
                table: "TestCases",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "testgen",
                table: "TestCases",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "testgen",
                table: "LlmSuggestions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeletedById",
                schema: "testgen",
                table: "LlmSuggestions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "testgen",
                table: "LlmSuggestions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_DeletedById",
                schema: "testgen",
                table: "TestCases",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_TestSuiteId_IsDeleted",
                schema: "testgen",
                table: "TestCases",
                columns: new[] { "TestSuiteId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestions_DeletedById",
                schema: "testgen",
                table: "LlmSuggestions",
                column: "DeletedById");

            migrationBuilder.CreateIndex(
                name: "IX_LlmSuggestions_TestSuiteId_IsDeleted",
                schema: "testgen",
                table: "LlmSuggestions",
                columns: new[] { "TestSuiteId", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TestCases_DeletedById",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_TestSuiteId_IsDeleted",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_LlmSuggestions_DeletedById",
                schema: "testgen",
                table: "LlmSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_LlmSuggestions_TestSuiteId_IsDeleted",
                schema: "testgen",
                table: "LlmSuggestions");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "testgen",
                table: "LlmSuggestions");

            migrationBuilder.DropColumn(
                name: "DeletedById",
                schema: "testgen",
                table: "LlmSuggestions");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "testgen",
                table: "LlmSuggestions");
        }
    }
}
