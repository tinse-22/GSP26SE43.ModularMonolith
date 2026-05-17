using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.ApiDocumentation
{
    /// <inheritdoc />
    public partial class AddEndpointParameterContentType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                schema: "apidoc",
                table: "EndpointParameters",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentType",
                schema: "apidoc",
                table: "EndpointParameters");
        }
    }
}
