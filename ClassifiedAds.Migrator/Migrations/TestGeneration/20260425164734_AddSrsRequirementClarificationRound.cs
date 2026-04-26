using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class AddSrsRequirementClarificationRound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "RefinedConfidenceScore",
                schema: "testgen",
                table: "SrsRequirements",
                type: "real",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RefinedConstraints",
                schema: "testgen",
                table: "SrsRequirements",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RefinementRound",
                schema: "testgen",
                table: "SrsRequirements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "JobType",
                schema: "testgen",
                table: "SrsAnalysisJobs",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SrsRequirementClarifications",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SrsRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    AmbiguitySource = table.Column<string>(type: "text", nullable: true),
                    Question = table.Column<string>(type: "text", nullable: false),
                    SuggestedOptions = table.Column<string>(type: "jsonb", nullable: true),
                    UserAnswer = table.Column<string>(type: "text", nullable: true),
                    IsAnswered = table.Column<bool>(type: "boolean", nullable: false),
                    AnsweredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AnsweredById = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsCritical = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SrsRequirementClarifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SrsRequirementClarifications_SrsRequirements_SrsRequirement~",
                        column: x => x.SrsRequirementId,
                        principalSchema: "testgen",
                        principalTable: "SrsRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SrsRequirementClarifications_AnsweredById",
                schema: "testgen",
                table: "SrsRequirementClarifications",
                column: "AnsweredById");

            migrationBuilder.CreateIndex(
                name: "IX_SrsRequirementClarifications_SrsRequirementId",
                schema: "testgen",
                table: "SrsRequirementClarifications",
                column: "SrsRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsRequirementClarifications_SrsRequirementId_DisplayOrder",
                schema: "testgen",
                table: "SrsRequirementClarifications",
                columns: new[] { "SrsRequirementId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SrsRequirementClarifications_SrsRequirementId_IsAnswered",
                schema: "testgen",
                table: "SrsRequirementClarifications",
                columns: new[] { "SrsRequirementId", "IsAnswered" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SrsRequirementClarifications",
                schema: "testgen");

            migrationBuilder.DropColumn(
                name: "RefinedConfidenceScore",
                schema: "testgen",
                table: "SrsRequirements");

            migrationBuilder.DropColumn(
                name: "RefinedConstraints",
                schema: "testgen",
                table: "SrsRequirements");

            migrationBuilder.DropColumn(
                name: "RefinementRound",
                schema: "testgen",
                table: "SrsRequirements");

            migrationBuilder.DropColumn(
                name: "JobType",
                schema: "testgen",
                table: "SrsAnalysisJobs");
        }
    }
}
