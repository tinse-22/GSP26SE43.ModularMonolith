using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class AddTestSuiteSelectedEndpointIds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SelectedEndpointIds",
                schema: "testgen",
                table: "TestSuites",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SelectedEndpointIds",
                schema: "testgen",
                table: "TestSuites");
        }
    }
}
