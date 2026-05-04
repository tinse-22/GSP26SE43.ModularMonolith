using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestExecution
{
    /// <inheritdoc />
    public partial class AddIsEphemeralToTestRun : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsEphemeral",
                schema: "testexecution",
                table: "TestRuns",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_TestRuns_IsEphemeral",
                schema: "testexecution",
                table: "TestRuns",
                column: "IsEphemeral");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TestRuns_IsEphemeral",
                schema: "testexecution",
                table: "TestRuns");

            migrationBuilder.DropColumn(
                name: "IsEphemeral",
                schema: "testexecution",
                table: "TestRuns");
        }
    }
}
