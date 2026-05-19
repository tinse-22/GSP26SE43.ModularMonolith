using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class AddExpectedProvenanceToTestCaseExpectations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExpectedProvenance",
                schema: "testgen",
                table: "TestCaseExpectations",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedProvenance",
                schema: "testgen",
                table: "TestCaseExpectations");
        }
    }
}
