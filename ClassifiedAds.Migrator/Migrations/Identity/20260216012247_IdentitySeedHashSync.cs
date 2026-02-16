using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Identity
{
    /// <inheritdoc />
    public partial class IdentitySeedHashSync : Migration
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
                value: "AQAAAAIAAYagAAAAEPfBZpUxae9Dzcv3f2lA5qOYSbJhxh5oYiVhS+j9Q7Rppm2ETqZUaEhWsOYisFocEA==");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "PasswordHash",
                value: "AQAAAAIAAYagAAAAEDlFqrwIpQDVVwXus3MatUkO1o3wq0iBqGqnXu5DkliD+ic2jmEAvoCCLoonjCzPdA==");
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
                value: "AQAAAAEAACcQAAAAEH+jsbWrH4v5LnT429AB0w/+I6Y05097m53Qq1CCj/Y9fPJ4pAtDwtmT4tk7TIfP9w==");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"),
                column: "PasswordHash",
                value: "AQAAAAEAACcQAAAAEOTpuUKQx8yQvn+SoGpxuzFRyYGEEIj799+xim2iti6zufqH4+py34yKlFIvZ2HdaA==");
        }
    }
}
