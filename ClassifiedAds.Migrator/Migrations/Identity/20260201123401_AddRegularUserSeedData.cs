using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddRegularUserSeedData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "identity",
                table: "Users",
                columns: new[] { "Id", "AccessFailedCount", "Auth0UserId", "AzureAdB2CUserId", "ConcurrencyStamp", "CreatedDateTime", "Email", "EmailConfirmed", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "SecurityStamp", "TwoFactorEnabled", "UpdatedDateTime", "UserName" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), 0, null, null, "d9665377-c512-5620-0bfc-b0394064gd69", new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "user@example.com", true, true, null, "USER@EXAMPLE.COM", "USER@EXAMPLE.COM", "AQAAAAIAAYagAAAAEKyT+qK4VcVGnZsJG3BzjQQv7nqXgvXZ7xgP5Wh8Y0vKzH8xz2Xz7qK4VcVGnZsJG3A=", null, false, "XYZPCRDAS3MJWQD5CSW2GWPRADBXEZIN", false, null, "user@example.com" });

            migrationBuilder.InsertData(
                schema: "identity",
                table: "UserRoles",
                columns: new[] { "Id", "CreatedDateTime", "RoleId", "UpdatedDateTime", "UserId" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), null, new Guid("00000000-0000-0000-0000-000000000002") });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "UserRoles",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));
        }
    }
}
