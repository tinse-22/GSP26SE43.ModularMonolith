using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class AddSrsDocumentAndRequirementTraceability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SrsDocumentId",
                schema: "testgen",
                table: "TestSuites",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryRequirementId",
                schema: "testgen",
                table: "TestCases",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SrsDocuments",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TestSuiteId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RawContent = table.Column<string>(type: "text", nullable: true),
                    StorageFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParsedMarkdown = table.Column<string>(type: "text", nullable: true),
                    AnalysisStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    AnalyzedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SrsDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SrsAnalysisJobs",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SrsDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TriggeredById = table.Column<Guid>(type: "uuid", nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TriggeredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequirementsExtracted = table.Column<int>(type: "integer", nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ErrorDetails = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SrsAnalysisJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SrsAnalysisJobs_SrsDocuments_SrsDocumentId",
                        column: x => x.SrsDocumentId,
                        principalSchema: "testgen",
                        principalTable: "SrsDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SrsRequirements",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    SrsDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequirementCode = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    RequirementType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TestableConstraints = table.Column<string>(type: "jsonb", nullable: true),
                    Assumptions = table.Column<string>(type: "jsonb", nullable: true),
                    Ambiguities = table.Column<string>(type: "jsonb", nullable: true),
                    ConfidenceScore = table.Column<float>(type: "real", nullable: true),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: true),
                    MappedEndpointPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsReviewed = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SrsRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SrsRequirements_SrsDocuments_SrsDocumentId",
                        column: x => x.SrsDocumentId,
                        principalSchema: "testgen",
                        principalTable: "SrsDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestCaseRequirementLinks",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SrsRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraceabilityScore = table.Column<float>(type: "real", nullable: true),
                    MappingRationale = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseRequirementLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCaseRequirementLinks_SrsRequirements_SrsRequirementId",
                        column: x => x.SrsRequirementId,
                        principalSchema: "testgen",
                        principalTable: "SrsRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TestCaseRequirementLinks_TestCases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestSuites_SrsDocumentId",
                schema: "testgen",
                table: "TestSuites",
                column: "SrsDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_PrimaryRequirementId",
                schema: "testgen",
                table: "TestCases",
                column: "PrimaryRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsAnalysisJobs_QueuedAt",
                schema: "testgen",
                table: "SrsAnalysisJobs",
                column: "QueuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SrsAnalysisJobs_SrsDocumentId",
                schema: "testgen",
                table: "SrsAnalysisJobs",
                column: "SrsDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsAnalysisJobs_SrsDocumentId_QueuedAt",
                schema: "testgen",
                table: "SrsAnalysisJobs",
                columns: new[] { "SrsDocumentId", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SrsAnalysisJobs_Status",
                schema: "testgen",
                table: "SrsAnalysisJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SrsAnalysisJobs_TriggeredById",
                schema: "testgen",
                table: "SrsAnalysisJobs",
                column: "TriggeredById");

            migrationBuilder.CreateIndex(
                name: "IX_SrsDocuments_CreatedById",
                schema: "testgen",
                table: "SrsDocuments",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_SrsDocuments_ProjectId",
                schema: "testgen",
                table: "SrsDocuments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsDocuments_ProjectId_AnalysisStatus",
                schema: "testgen",
                table: "SrsDocuments",
                columns: new[] { "ProjectId", "AnalysisStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_SrsDocuments_ProjectId_IsDeleted",
                schema: "testgen",
                table: "SrsDocuments",
                columns: new[] { "ProjectId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_SrsDocuments_TestSuiteId",
                schema: "testgen",
                table: "SrsDocuments",
                column: "TestSuiteId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsRequirements_EndpointId",
                schema: "testgen",
                table: "SrsRequirements",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsRequirements_ReviewedById",
                schema: "testgen",
                table: "SrsRequirements",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_SrsRequirements_SrsDocumentId",
                schema: "testgen",
                table: "SrsRequirements",
                column: "SrsDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_SrsRequirements_SrsDocumentId_DisplayOrder",
                schema: "testgen",
                table: "SrsRequirements",
                columns: new[] { "SrsDocumentId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_SrsRequirements_SrsDocumentId_RequirementCode",
                schema: "testgen",
                table: "SrsRequirements",
                columns: new[] { "SrsDocumentId", "RequirementCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRequirementLinks_SrsRequirementId",
                schema: "testgen",
                table: "TestCaseRequirementLinks",
                column: "SrsRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRequirementLinks_TestCaseId_SrsRequirementId",
                schema: "testgen",
                table: "TestCaseRequirementLinks",
                columns: new[] { "TestCaseId", "SrsRequirementId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TestCases_SrsRequirements_PrimaryRequirementId",
                schema: "testgen",
                table: "TestCases",
                column: "PrimaryRequirementId",
                principalSchema: "testgen",
                principalTable: "SrsRequirements",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TestSuites_SrsDocuments_SrsDocumentId",
                schema: "testgen",
                table: "TestSuites",
                column: "SrsDocumentId",
                principalSchema: "testgen",
                principalTable: "SrsDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TestCases_SrsRequirements_PrimaryRequirementId",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.DropForeignKey(
                name: "FK_TestSuites_SrsDocuments_SrsDocumentId",
                schema: "testgen",
                table: "TestSuites");

            migrationBuilder.DropTable(
                name: "SrsAnalysisJobs",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "TestCaseRequirementLinks",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "SrsRequirements",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "SrsDocuments",
                schema: "testgen");

            migrationBuilder.DropIndex(
                name: "IX_TestSuites_SrsDocumentId",
                schema: "testgen",
                table: "TestSuites");

            migrationBuilder.DropIndex(
                name: "IX_TestCases_PrimaryRequirementId",
                schema: "testgen",
                table: "TestCases");

            migrationBuilder.DropColumn(
                name: "SrsDocumentId",
                schema: "testgen",
                table: "TestSuites");

            migrationBuilder.DropColumn(
                name: "PrimaryRequirementId",
                schema: "testgen",
                table: "TestCases");
        }
    }
}
