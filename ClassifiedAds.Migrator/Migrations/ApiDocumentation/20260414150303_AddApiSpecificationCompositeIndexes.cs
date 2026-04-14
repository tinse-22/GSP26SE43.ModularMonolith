using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.ApiDocumentation
{
    /// <inheritdoc />
    public partial class AddApiSpecificationCompositeIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ApiSpecifications_ProjectId_IsActive",
                schema: "apidoc",
                table: "ApiSpecifications",
                columns: new[] { "ProjectId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiSpecifications_ProjectId_IsDeleted_CreatedDateTime",
                schema: "apidoc",
                table: "ApiSpecifications",
                columns: new[] { "ProjectId", "IsDeleted", "CreatedDateTime" },
                descending: new[] { false, false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiSpecifications_ProjectId_IsActive",
                schema: "apidoc",
                table: "ApiSpecifications");

            migrationBuilder.DropIndex(
                name: "IX_ApiSpecifications_ProjectId_IsDeleted_CreatedDateTime",
                schema: "apidoc",
                table: "ApiSpecifications");
        }
    }
}
