using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class SyncTestGenerationExpectedAssertionModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpectationSource",
                schema: "testgen",
                table: "TestCaseExpectations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryRequirementId",
                schema: "testgen",
                table: "TestCaseExpectations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequirementCode",
                schema: "testgen",
                table: "TestCaseExpectations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectationSource",
                schema: "testgen",
                table: "TestCaseExpectations");

            migrationBuilder.DropColumn(
                name: "PrimaryRequirementId",
                schema: "testgen",
                table: "TestCaseExpectations");

            migrationBuilder.DropColumn(
                name: "RequirementCode",
                schema: "testgen",
                table: "TestCaseExpectations");
        }
    }
}
