using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestExecution
{
    /// <inheritdoc />
    public partial class AddExpectedProvenanceToTestCaseResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpectedProvenance",
                schema: "testexecution",
                table: "TestCaseResults",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedProvenance",
                schema: "testexecution",
                table: "TestCaseResults");
        }
    }
}
