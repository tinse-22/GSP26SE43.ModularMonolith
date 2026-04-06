using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClassifiedAds.Migrator.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddUserStoragePermissionsAndRoleDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000035"),
                column: "Value",
                value: "Permission:GetFiles");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000036"),
                column: "Value",
                value: "Permission:UploadFile");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000037"),
                column: "Value",
                value: "Permission:GetFile");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000038"),
                column: "Value",
                value: "Permission:DownloadFile");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000039"),
                column: "Value",
                value: "Permission:UpdateFile");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000040"),
                column: "Value",
                value: "Permission:DeleteFile");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000041"),
                column: "Value",
                value: "Permission:GetFileAuditLogs");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000042"),
                column: "Value",
                value: "Permission:GetPlans");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000043"),
                column: "Value",
                value: "Permission:GetSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000044"),
                column: "Value",
                value: "Permission:GetCurrentSubscription");

            migrationBuilder.InsertData(
                schema: "identity",
                table: "RoleClaims",
                columns: new[] { "Id", "CreatedDateTime", "RoleId", "Type", "UpdatedDateTime", "Value" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0002-000000000045"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:CancelSubscription" },
                    { new Guid("00000000-0000-0000-0002-000000000046"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetSubscriptionHistory" },
                    { new Guid("00000000-0000-0000-0002-000000000047"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetPaymentTransactions" },
                    { new Guid("00000000-0000-0000-0002-000000000048"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetUsageTracking" },
                    { new Guid("00000000-0000-0000-0002-000000000049"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:CreateSubscriptionPayment" },
                    { new Guid("00000000-0000-0000-0002-000000000050"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetPaymentIntent" },
                    { new Guid("00000000-0000-0000-0002-000000000051"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:CreatePayOsCheckout" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000045"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000046"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000047"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000048"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000049"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000050"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000051"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000035"),
                column: "Value",
                value: "Permission:GetPlans");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000036"),
                column: "Value",
                value: "Permission:GetSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000037"),
                column: "Value",
                value: "Permission:GetCurrentSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000038"),
                column: "Value",
                value: "Permission:CancelSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000039"),
                column: "Value",
                value: "Permission:GetSubscriptionHistory");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000040"),
                column: "Value",
                value: "Permission:GetPaymentTransactions");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000041"),
                column: "Value",
                value: "Permission:GetUsageTracking");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000042"),
                column: "Value",
                value: "Permission:CreateSubscriptionPayment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000043"),
                column: "Value",
                value: "Permission:GetPaymentIntent");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000044"),
                column: "Value",
                value: "Permission:CreatePayOsCheckout");
        }
    }
}
