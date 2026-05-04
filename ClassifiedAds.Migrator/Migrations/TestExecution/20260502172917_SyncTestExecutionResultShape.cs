using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestExecution
{
    /// <inheritdoc />
    public partial class SyncTestExecutionResultShape : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpectationSource",
                schema: "testexecution",
                table: "TestCaseResults",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryRequirementId",
                schema: "testexecution",
                table: "TestCaseResults",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequirementCode",
                schema: "testexecution",
                table: "TestCaseResults",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectationSource",
                schema: "testexecution",
                table: "TestCaseResults");

            migrationBuilder.DropColumn(
                name: "PrimaryRequirementId",
                schema: "testexecution",
                table: "TestCaseResults");

            migrationBuilder.DropColumn(
                name: "RequirementCode",
                schema: "testexecution",
                table: "TestCaseResults");
        }
    }
}
