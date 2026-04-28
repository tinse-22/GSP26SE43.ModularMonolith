using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClassifiedAds.Migrator.Migrations.Identity
{
    /// <inheritdoc />
    public partial class AddSrsTraceabilityPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000061"),
                column: "Value",
                value: "Permission:GetSrsDocuments");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000062"),
                column: "Value",
                value: "Permission:AddSrsDocument");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000063"),
                column: "Value",
                value: "Permission:DeleteSrsDocument");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000064"),
                column: "Value",
                value: "Permission:TriggerSrsAnalysis");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000065"),
                column: "Value",
                value: "Permission:ManageSrsRequirements");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000066"),
                column: "Value",
                value: "Permission:GetSrsTraceability");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000067"),
                column: "Value",
                value: "Permission:ManageTraceabilityLinks");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000068"),
                column: "Value",
                value: "Permission:GetExecutionEnvironments");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000069"),
                column: "Value",
                value: "Permission:AddExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000070"),
                column: "Value",
                value: "Permission:UpdateExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000071"),
                column: "Value",
                value: "Permission:DeleteExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000072"),
                column: "Value",
                value: "Permission:StartTestRun");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000073"),
                column: "Value",
                value: "Permission:GetTestRuns");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000074"),
                column: "Value",
                value: "Permission:GetPlans");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000075"),
                column: "Value",
                value: "Permission:AddPlan");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000076"),
                column: "Value",
                value: "Permission:UpdatePlan");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000077"),
                column: "Value",
                value: "Permission:DeletePlan");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000078"),
                column: "Value",
                value: "Permission:GetPlanAuditLogs");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000079"),
                column: "Value",
                value: "Permission:GetSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000080"),
                column: "Value",
                value: "Permission:GetCurrentSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000081"),
                column: "Value",
                value: "Permission:AddSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000082"),
                column: "Value",
                value: "Permission:UpdateSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000083"),
                column: "Value",
                value: "Permission:CancelSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000084"),
                column: "Value",
                value: "Permission:GetSubscriptionHistory");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000085"),
                column: "Value",
                value: "Permission:GetPaymentTransactions");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000029"),
                column: "Value",
                value: "Permission:GetSrsDocuments");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000030"),
                column: "Value",
                value: "Permission:AddSrsDocument");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000031"),
                column: "Value",
                value: "Permission:DeleteSrsDocument");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000032"),
                column: "Value",
                value: "Permission:TriggerSrsAnalysis");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000033"),
                column: "Value",
                value: "Permission:ManageSrsRequirements");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000034"),
                column: "Value",
                value: "Permission:GetSrsTraceability");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000035"),
                column: "Value",
                value: "Permission:ManageTraceabilityLinks");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000036"),
                column: "Value",
                value: "Permission:GetExecutionEnvironments");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000037"),
                column: "Value",
                value: "Permission:AddExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000038"),
                column: "Value",
                value: "Permission:UpdateExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000039"),
                column: "Value",
                value: "Permission:DeleteExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000040"),
                column: "Value",
                value: "Permission:StartTestRun");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000041"),
                column: "Value",
                value: "Permission:GetTestRuns");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000042"),
                column: "Value",
                value: "Permission:GetFiles");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000043"),
                column: "Value",
                value: "Permission:UploadFile");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000044"),
                column: "Value",
                value: "Permission:GetFile");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000045"),
                column: "Value",
                value: "Permission:DownloadFile");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000046"),
                column: "Value",
                value: "Permission:UpdateFile");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000047"),
                column: "Value",
                value: "Permission:DeleteFile");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000048"),
                column: "Value",
                value: "Permission:GetFileAuditLogs");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000049"),
                column: "Value",
                value: "Permission:GetPlans");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000050"),
                column: "Value",
                value: "Permission:GetSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000051"),
                column: "Value",
                value: "Permission:GetCurrentSubscription");

            migrationBuilder.InsertData(
                schema: "identity",
                table: "RoleClaims",
                columns: new[] { "Id", "CreatedDateTime", "RoleId", "Type", "UpdatedDateTime", "Value" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000086"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddPaymentTransaction" },
                    { new Guid("00000000-0000-0000-0001-000000000087"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetUsageTracking" },
                    { new Guid("00000000-0000-0000-0001-000000000088"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateUsageTracking" },
                    { new Guid("00000000-0000-0000-0001-000000000089"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:CreateSubscriptionPayment" },
                    { new Guid("00000000-0000-0000-0001-000000000090"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetPaymentIntent" },
                    { new Guid("00000000-0000-0000-0001-000000000091"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:CreatePayOsCheckout" },
                    { new Guid("00000000-0000-0000-0001-000000000092"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:SyncPayment" },
                    { new Guid("00000000-0000-0000-0002-000000000052"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:CancelSubscription" },
                    { new Guid("00000000-0000-0000-0002-000000000053"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetSubscriptionHistory" },
                    { new Guid("00000000-0000-0000-0002-000000000054"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetPaymentTransactions" },
                    { new Guid("00000000-0000-0000-0002-000000000055"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetUsageTracking" },
                    { new Guid("00000000-0000-0000-0002-000000000056"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:CreateSubscriptionPayment" },
                    { new Guid("00000000-0000-0000-0002-000000000057"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetPaymentIntent" },
                    { new Guid("00000000-0000-0000-0002-000000000058"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:CreatePayOsCheckout" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000086"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000087"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000088"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000089"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000090"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000091"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000092"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000052"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000053"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000054"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000055"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000056"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000057"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000058"));

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000061"),
                column: "Value",
                value: "Permission:GetExecutionEnvironments");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000062"),
                column: "Value",
                value: "Permission:AddExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000063"),
                column: "Value",
                value: "Permission:UpdateExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000064"),
                column: "Value",
                value: "Permission:DeleteExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000065"),
                column: "Value",
                value: "Permission:StartTestRun");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000066"),
                column: "Value",
                value: "Permission:GetTestRuns");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000067"),
                column: "Value",
                value: "Permission:GetPlans");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000068"),
                column: "Value",
                value: "Permission:AddPlan");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000069"),
                column: "Value",
                value: "Permission:UpdatePlan");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000070"),
                column: "Value",
                value: "Permission:DeletePlan");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000071"),
                column: "Value",
                value: "Permission:GetPlanAuditLogs");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000072"),
                column: "Value",
                value: "Permission:GetSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000073"),
                column: "Value",
                value: "Permission:GetCurrentSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000074"),
                column: "Value",
                value: "Permission:AddSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000075"),
                column: "Value",
                value: "Permission:UpdateSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000076"),
                column: "Value",
                value: "Permission:CancelSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000077"),
                column: "Value",
                value: "Permission:GetSubscriptionHistory");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000078"),
                column: "Value",
                value: "Permission:GetPaymentTransactions");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000079"),
                column: "Value",
                value: "Permission:AddPaymentTransaction");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000080"),
                column: "Value",
                value: "Permission:GetUsageTracking");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000081"),
                column: "Value",
                value: "Permission:UpdateUsageTracking");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000082"),
                column: "Value",
                value: "Permission:CreateSubscriptionPayment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000083"),
                column: "Value",
                value: "Permission:GetPaymentIntent");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000084"),
                column: "Value",
                value: "Permission:CreatePayOsCheckout");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000085"),
                column: "Value",
                value: "Permission:SyncPayment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000029"),
                column: "Value",
                value: "Permission:GetExecutionEnvironments");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000030"),
                column: "Value",
                value: "Permission:AddExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000031"),
                column: "Value",
                value: "Permission:UpdateExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000032"),
                column: "Value",
                value: "Permission:DeleteExecutionEnvironment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000033"),
                column: "Value",
                value: "Permission:StartTestRun");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000034"),
                column: "Value",
                value: "Permission:GetTestRuns");

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

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000045"),
                column: "Value",
                value: "Permission:CancelSubscription");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000046"),
                column: "Value",
                value: "Permission:GetSubscriptionHistory");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000047"),
                column: "Value",
                value: "Permission:GetPaymentTransactions");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000048"),
                column: "Value",
                value: "Permission:GetUsageTracking");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000049"),
                column: "Value",
                value: "Permission:CreateSubscriptionPayment");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000050"),
                column: "Value",
                value: "Permission:GetPaymentIntent");

            migrationBuilder.UpdateData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000051"),
                column: "Value",
                value: "Permission:CreatePayOsCheckout");
        }
    }
}
