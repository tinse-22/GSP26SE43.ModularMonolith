using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Identity
{
    /// <inheritdoc />
    public partial class FixPasswordHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "PasswordHash",
                value: "AQAAAAEAACcQAAAAEH+jsbWrH4v5LnT429AB0w/+I6Y05097m53Qq1CCj/Y9fPJ4pAtDwtmT4tk7TIfP9w==");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "PasswordHash",
                value: "AQAAAAEAACcQAAAAEOTpuUKQx8yQvn+SoGpxuzFRyYGEEIj799+xim2iti6zufqH4+py34yKlFIvZ2HdaA==");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEKyT+qK4VcVGnZsJG3BzjQQv7nqXgvXZ7xgP5Wh8Y0vKzH8xz2Xz7qK4VcVGnZsJG3A=");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEKyT+qK4VcVGnZsJG3BzjQQv7nqXgvXZ7xgP5Wh8Y0vKzH8xz2Xz7qK4VcVGnZsJG3A=");
        }
    }
}
