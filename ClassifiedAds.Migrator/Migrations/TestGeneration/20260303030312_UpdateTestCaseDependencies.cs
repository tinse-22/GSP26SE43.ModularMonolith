using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class UpdateTestCaseDependencies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_TestCases_DependsOnId",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_DependsOnId",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "DependsOnId",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.CreateTable(
                name: "TestCaseDependencies",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnTestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCaseDependencies_TestCases_DependsOnTestCaseId",
                        column: x => x.DependsOnTestCaseId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestCaseDependencies_TestCases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseDependencies_DependsOnTestCaseId",
                schema: "testgen",
                table: "TestCaseDependencies",
                column: "DependsOnTestCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseDependencies_TestCaseId_DependsOnTestCaseId",
                schema: "testgen",
                table: "TestCaseDependencies",
                columns: new[] { "TestCaseId", "DependsOnTestCaseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TestCaseDependencies",
                schema: "testgen");

            migrationBuilder.AddColumn<Guid>(
                name: "DependsOnId",
                schema: "testgen",
                table: "TestCases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_DependsOnId",
                schema: "testgen",
                table: "TestCases",
                column: "DependsOnId");

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_TestCases_DependsOnId",
                schema: "testgen",
                table: "TestCases",
                column: "DependsOnId",
                principalSchema: "testgen",
                principalTable: "TestCases",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
