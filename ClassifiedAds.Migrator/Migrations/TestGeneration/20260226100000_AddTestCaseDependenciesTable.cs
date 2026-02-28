using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class AddTestCaseDependenciesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Create the new TestCaseDependencies join table
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
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCaseDependencies_TestCases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestCaseDependencies_TestCases_DependsOnTestCaseId",
                        column: x => x.DependsOnTestCaseId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseDependencies_TestCaseId_DependsOnTestCaseId",
                schema: "testgen",
                table: "TestCaseDependencies",
                columns: new[] { "TestCaseId", "DependsOnTestCaseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseDependencies_DependsOnTestCaseId",
                schema: "testgen",
                table: "TestCaseDependencies",
                column: "DependsOnTestCaseId");

            // 2) Migrate existing DependsOnId data to the new join table
            migrationBuilder.Sql(@"
                INSERT INTO testgen.""TestCaseDependencies"" (""Id"", ""TestCaseId"", ""DependsOnTestCaseId"", ""CreatedDateTime"")
                SELECT gen_random_uuid(), ""Id"", ""DependsOnId"", NOW()
                FROM testgen.""TestCases""
                WHERE ""DependsOnId"" IS NOT NULL;
            ");

            // 3) Drop the old self-referencing FK and index
            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_TestCases_DependsOnId",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_DependsOnId",
                schema: "testgen",
                table: "TestCases");

            // 4) Drop the old DependsOnId column
            migrationBuilder.DropColumn(
                name: "DependsOnId",
                schema: "testgen",
                table: "TestCases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1) Re-add the DependsOnId column
            migrationBuilder.AddColumn<Guid>(
                name: "DependsOnId",
                schema: "testgen",
                table: "TestCases",
                type: "uuid",
                nullable: true);

            // 2) Migrate data back (take first dependency per test case)
            migrationBuilder.Sql(@"
                UPDATE testgen.""TestCases"" tc
                SET ""DependsOnId"" = sub.""DependsOnTestCaseId""
                FROM (
                    SELECT DISTINCT ON (""TestCaseId"") ""TestCaseId"", ""DependsOnTestCaseId""
                    FROM testgen.""TestCaseDependencies""
                    ORDER BY ""TestCaseId"", ""CreatedDateTime""
                ) sub
                WHERE tc.""Id"" = sub.""TestCaseId"";
            ");

            // 3) Re-create the old index and FK
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

            // 4) Drop the new join table
            migrationBuilder.DropTable(
                name: "TestCaseDependencies",
                schema: "testgen");
        }
    }
}
