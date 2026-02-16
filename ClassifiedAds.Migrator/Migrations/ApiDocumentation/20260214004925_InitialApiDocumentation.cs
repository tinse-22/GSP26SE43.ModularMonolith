using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.ApiDocumentation
{
    /// <inheritdoc />
    public partial class InitialApiDocumentation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "apidoc");

            migrationBuilder.CreateTable(
                name: "ArchivedOutboxMessages",
                schema: "apidoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_ArchivedOutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogEntries",
                schema: "apidoc",
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
                schema: "apidoc",
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
                name: "ApiEndpoints",
                schema: "apidoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ApiSpecId = table.Column<Guid>(type: "uuid", nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    OperationId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Tags = table.Column<string>(type: "jsonb", nullable: true),
                    IsDeprecated = table.Column<bool>(type: "boolean", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiEndpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EndpointParameters",
                schema: "apidoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Location = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DataType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultValue = table.Column<string>(type: "text", nullable: true),
                    Schema = table.Column<string>(type: "jsonb", nullable: true),
                    Examples = table.Column<string>(type: "jsonb", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndpointParameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EndpointParameters_ApiEndpoints_EndpointId",
                        column: x => x.EndpointId,
                        principalSchema: "apidoc",
                        principalTable: "ApiEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EndpointResponses",
                schema: "apidoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Schema = table.Column<string>(type: "jsonb", nullable: true),
                    Examples = table.Column<string>(type: "jsonb", nullable: true),
                    Headers = table.Column<string>(type: "jsonb", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndpointResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EndpointResponses_ApiEndpoints_EndpointId",
                        column: x => x.EndpointId,
                        principalSchema: "apidoc",
                        principalTable: "ApiEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EndpointSecurityReqs",
                schema: "apidoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    EndpointId = table.Column<Guid>(type: "uuid", nullable: false),
                    SecurityType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SchemeName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Scopes = table.Column<string>(type: "jsonb", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EndpointSecurityReqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EndpointSecurityReqs_ApiEndpoints_EndpointId",
                        column: x => x.EndpointId,
                        principalSchema: "apidoc",
                        principalTable: "ApiEndpoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiSpecifications",
                schema: "apidoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalFileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ParsedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ParseStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ParseErrors = table.Column<string>(type: "jsonb", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiSpecifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "apidoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActiveSpecId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_ApiSpecifications_ActiveSpecId",
                        column: x => x.ActiveSpecId,
                        principalSchema: "apidoc",
                        principalTable: "ApiSpecifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SecuritySchemes",
                schema: "apidoc",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    ApiSpecId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Scheme = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BearerFormat = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    In = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ParameterName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Configuration = table.Column<string>(type: "jsonb", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecuritySchemes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecuritySchemes_ApiSpecifications_ApiSpecId",
                        column: x => x.ApiSpecId,
                        principalSchema: "apidoc",
                        principalTable: "ApiSpecifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiEndpoints_ApiSpecId",
                schema: "apidoc",
                table: "ApiEndpoints",
                column: "ApiSpecId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiEndpoints_ApiSpecId_HttpMethod_Path",
                schema: "apidoc",
                table: "ApiEndpoints",
                columns: new[] { "ApiSpecId", "HttpMethod", "Path" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiSpecifications_IsActive",
                schema: "apidoc",
                table: "ApiSpecifications",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ApiSpecifications_ProjectId",
                schema: "apidoc",
                table: "ApiSpecifications",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ArchivedOutboxMessages_CreatedDateTime",
                schema: "apidoc",
                table: "ArchivedOutboxMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_EndpointParameters_EndpointId",
                schema: "apidoc",
                table: "EndpointParameters",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_EndpointResponses_EndpointId",
                schema: "apidoc",
                table: "EndpointResponses",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_EndpointResponses_EndpointId_StatusCode",
                schema: "apidoc",
                table: "EndpointResponses",
                columns: new[] { "EndpointId", "StatusCode" });

            migrationBuilder.CreateIndex(
                name: "IX_EndpointSecurityReqs_EndpointId",
                schema: "apidoc",
                table: "EndpointSecurityReqs",
                column: "EndpointId");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CreatedDateTime",
                schema: "apidoc",
                table: "OutboxMessages",
                column: "CreatedDateTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Published_CreatedDateTime",
                schema: "apidoc",
                table: "OutboxMessages",
                columns: new[] { "Published", "CreatedDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ActiveSpecId",
                schema: "apidoc",
                table: "Projects",
                column: "ActiveSpecId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OwnerId",
                schema: "apidoc",
                table: "Projects",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Status",
                schema: "apidoc",
                table: "Projects",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SecuritySchemes_ApiSpecId",
                schema: "apidoc",
                table: "SecuritySchemes",
                column: "ApiSpecId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiEndpoints_ApiSpecifications_ApiSpecId",
                schema: "apidoc",
                table: "ApiEndpoints",
                column: "ApiSpecId",
                principalSchema: "apidoc",
                principalTable: "ApiSpecifications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ApiSpecifications_Projects_ProjectId",
                schema: "apidoc",
                table: "ApiSpecifications",
                column: "ProjectId",
                principalSchema: "apidoc",
                principalTable: "Projects",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ApiSpecifications_ActiveSpecId",
                schema: "apidoc",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ArchivedOutboxMessages",
                schema: "apidoc");

            migrationBuilder.DropTable(
                name: "AuditLogEntries",
                schema: "apidoc");

            migrationBuilder.DropTable(
                name: "EndpointParameters",
                schema: "apidoc");

            migrationBuilder.DropTable(
                name: "EndpointResponses",
                schema: "apidoc");

            migrationBuilder.DropTable(
                name: "EndpointSecurityReqs",
                schema: "apidoc");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "apidoc");

            migrationBuilder.DropTable(
                name: "SecuritySchemes",
                schema: "apidoc");

            migrationBuilder.DropTable(
                name: "ApiEndpoints",
                schema: "apidoc");

            migrationBuilder.DropTable(
                name: "ApiSpecifications",
                schema: "apidoc");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "apidoc");
        }
    }
}
