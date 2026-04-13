using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.ApiDocumentation
{
    /// <inheritdoc />
    public partial class AddApiSpecificationSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "apidoc",
                table: "ApiSpecifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                schema: "apidoc",
                table: "ApiSpecifications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ApiSpecifications_IsDeleted",
                schema: "apidoc",
                table: "ApiSpecifications",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiSpecifications_IsDeleted",
                schema: "apidoc",
                table: "ApiSpecifications");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "apidoc",
                table: "ApiSpecifications");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                schema: "apidoc",
                table: "ApiSpecifications");
        }
    }
}
