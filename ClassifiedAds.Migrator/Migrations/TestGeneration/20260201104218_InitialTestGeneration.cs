using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.TestGeneration
{
    /// <inheritdoc />
    public partial class InitialTestGeneration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "testgen");

            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: true),
                    ObjectId = table.Column<string>(type: "text", nullable: true),
                    Log = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EventType = table.Column<string>(type: "text", nullable: true),
                    TriggeredById = table.Column<Guid>(type: "uuid", nullable: false),
                    ObjectId = table.Column<string>(type: "text", nullable: true),
                    Payload = table.Column<string>(type: "text", nullable: true),
                    Published = table.Column<bool>(type: "boolean", nullable: false),
                    ActivityId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestSuites",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApiSpecId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    GenerationType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovalStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ApprovedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ApprovedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    LastModifiedById = table.Column<Guid>(type: "uuid", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestSuites", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestCases",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    TestType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Priority = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    DependsOnId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CustomOrderIndex = table.Column<int>(type: "integer", nullable: true),
                    IsOrderCustomized = table.Column<bool>(type: "boolean", nullable: false),
                    Tags = table.Column<string>(type: "jsonb", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uuid", nullable: true),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCases_TestCases_DependsOnId",
                        column: x => x.DependsOnId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TestCases_TestSuites_TestSuiteId",
                        column: x => x.TestSuiteId,
                        principalSchema: "testgen",
                        principalTable: "TestSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestOrderProposals",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProposalNumber = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProposedOrder = table.Column<string>(type: "jsonb", nullable: false),
                    AiReasoning = table.Column<string>(type: "text", nullable: true),
                    ConsideredFactors = table.Column<string>(type: "jsonb", nullable: true),
                    ReviewedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewNotes = table.Column<string>(type: "text", nullable: true),
                    UserModifiedOrder = table.Column<string>(type: "jsonb", nullable: true),
                    AppliedOrder = table.Column<string>(type: "jsonb", nullable: true),
                    AppliedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LlmModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TokensUsed = table.Column<int>(type: "integer", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestOrderProposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestOrderProposals_TestSuites_TestSuiteId",
                        column: x => x.TestSuiteId,
                        principalSchema: "testgen",
                        principalTable: "TestSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestSuiteVersions",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestSuiteId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    ChangedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ChangeDescription = table.Column<string>(type: "text", nullable: true),
                    TestCaseOrderSnapshot = table.Column<string>(type: "jsonb", nullable: true),
                    ApprovalStatusSnapshot = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PreviousState = table.Column<string>(type: "jsonb", nullable: true),
                    NewState = table.Column<string>(type: "jsonb", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestSuiteVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestSuiteVersions_TestSuites_TestSuiteId",
                        column: x => x.TestSuiteId,
                        principalSchema: "testgen",
                        principalTable: "TestSuites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestCaseChangeLogs",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedById = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FieldName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    OldValue = table.Column<string>(type: "text", nullable: true),
                    NewValue = table.Column<string>(type: "text", nullable: true),
                    ChangeReason = table.Column<string>(type: "text", nullable: true),
                    VersionAfterChange = table.Column<int>(type: "integer", nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCaseChangeLogs_TestCases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestCaseExpectations",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedStatus = table.Column<string>(type: "jsonb", nullable: true),
                    ResponseSchema = table.Column<string>(type: "jsonb", nullable: true),
                    HeaderChecks = table.Column<string>(type: "jsonb", nullable: true),
                    BodyContains = table.Column<string>(type: "jsonb", nullable: true),
                    BodyNotContains = table.Column<string>(type: "jsonb", nullable: true),
                    JsonPathChecks = table.Column<string>(type: "jsonb", nullable: true),
                    MaxResponseTime = table.Column<int>(type: "integer", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseExpectations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCaseExpectations_TestCases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestCaseRequests",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Headers = table.Column<string>(type: "jsonb", nullable: true),
                    PathParams = table.Column<string>(type: "jsonb", nullable: true),
                    QueryParams = table.Column<string>(type: "jsonb", nullable: true),
                    BodyType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: true),
                    Timeout = table.Column<int>(type: "integer", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCaseRequests_TestCases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestCaseVariables",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    VariableName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExtractFrom = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    JsonPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    HeaderName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Regex = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DefaultValue = table.Column<string>(type: "text", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestCaseVariables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestCaseVariables_TestCases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TestDataSets",
                schema: "testgen",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    TestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Data = table.Column<string>(type: "jsonb", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestDataSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TestDataSets_TestCases_TestCaseId",
                        column: x => x.TestCaseId,
                        principalSchema: "testgen",
                        principalTable: "TestCases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseChangeLogs_ChangedById",
                schema: "testgen",
                table: "TestCaseChangeLogs",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseChangeLogs_ChangeType",
                schema: "testgen",
                table: "TestCaseChangeLogs",
                column: "ChangeType");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseChangeLogs_CreatedDateTime",
                schema: "testgen",
                table: "TestCaseChangeLogs",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseChangeLogs_TestCaseId",
                schema: "testgen",
                table: "TestCaseChangeLogs",
                column: "TestCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseExpectations_TestCaseId",
                schema: "testgen",
                table: "TestCaseExpectations",
                column: "TestCaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseRequests_TestCaseId",
                schema: "testgen",
                table: "TestCaseRequests",
                column: "TestCaseId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_DependsOnId",
                schema: "testgen",
                table: "TestCases",
                column: "DependsOnId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_EndpointId",
                schema: "testgen",
                table: "TestCases",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_LastModifiedById",
                schema: "testgen",
                table: "TestCases",
                column: "LastModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_TestSuiteId",
                schema: "testgen",
                table: "TestCases",
                column: "TestSuiteId");

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_TestSuiteId_CustomOrderIndex",
                schema: "testgen",
                table: "TestCases",
                columns: new[] { "TestSuiteId", "CustomOrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TestCases_TestSuiteId_OrderIndex",
                schema: "testgen",
                table: "TestCases",
                columns: new[] { "TestSuiteId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_TestCaseVariables_TestCaseId",
                schema: "testgen",
                table: "TestCaseVariables",
                column: "TestCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TestDataSets_TestCaseId",
                schema: "testgen",
                table: "TestDataSets",
                column: "TestCaseId");

            migrationBuilder.CreateIndex(
                name: "IX_TestOrderProposals_ReviewedById",
                schema: "testgen",
                table: "TestOrderProposals",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestOrderProposals_Source",
                schema: "testgen",
                table: "TestOrderProposals",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_TestOrderProposals_Status",
                schema: "testgen",
                table: "TestOrderProposals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TestOrderProposals_TestSuiteId",
                schema: "testgen",
                table: "TestOrderProposals",
                column: "TestSuiteId");

            migrationBuilder.CreateIndex(
                name: "IX_TestOrderProposals_TestSuiteId_ProposalNumber",
                schema: "testgen",
                table: "TestOrderProposals",
                columns: new[] { "TestSuiteId", "ProposalNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_TestSuites_ApiSpecId",
                schema: "testgen",
                table: "TestSuites",
                column: "ApiSpecId");

            migrationBuilder.CreateIndex(
                name: "IX_TestSuites_ApprovalStatus",
                schema: "testgen",
                table: "TestSuites",
                column: "ApprovalStatus");

            migrationBuilder.CreateIndex(
                name: "IX_TestSuites_ApprovedById",
                schema: "testgen",
                table: "TestSuites",
                column: "ApprovedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestSuites_CreatedById",
                schema: "testgen",
                table: "TestSuites",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestSuites_LastModifiedById",
                schema: "testgen",
                table: "TestSuites",
                column: "LastModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestSuites_ProjectId",
                schema: "testgen",
                table: "TestSuites",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TestSuites_Status",
                schema: "testgen",
                table: "TestSuites",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TestSuiteVersions_ChangedById",
                schema: "testgen",
                table: "TestSuiteVersions",
                column: "ChangedById");

            migrationBuilder.CreateIndex(
                name: "IX_TestSuiteVersions_ChangeType",
                schema: "testgen",
                table: "TestSuiteVersions",
                column: "ChangeType");

            migrationBuilder.CreateIndex(
                name: "IX_TestSuiteVersions_TestSuiteId",
                schema: "testgen",
                table: "TestSuiteVersions",
                column: "TestSuiteId");

            migrationBuilder.CreateIndex(
                name: "IX_TestSuiteVersions_TestSuiteId_VersionNumber",
                schema: "testgen",
                table: "TestSuiteVersions",
                columns: new[] { "TestSuiteId", "VersionNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogEntries",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "TestCaseChangeLogs",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "TestCaseExpectations",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "TestCaseRequests",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "TestCaseVariables",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "TestDataSets",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "TestOrderProposals",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "TestSuiteVersions",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "TestCases",
                schema: "testgen");

            migrationBuilder.DropTable(
                name: "TestSuites",
                schema: "testgen");
        }
    }
}
