using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestExecution
{
    /// <inheritdoc />
    public partial class AddExpectedStatusToTestCaseResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpectedStatus",
                schema: "testexecution",
                table: "TestCaseResults",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedStatus",
                schema: "testexecution",
                table: "TestCaseResults");
        }
    }
}
