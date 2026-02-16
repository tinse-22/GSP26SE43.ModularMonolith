using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClassifiedAds.Migrator.Migrations.Subscription
{
    /// <inheritdoc />
    public partial class SeedSubscriptionPlansAndLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "subscription",
                table: "SubscriptionPlans",
                columns: new[] { "Id", "CreatedDateTime", "Currency", "Description", "DisplayName", "IsActive", "Name", "PriceMonthly", "PriceYearly", "SortOrder", "UpdatedDateTime" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "VND", "Basic plan for individuals getting started", "Free", true, "Free", 0m, 0m, 1, null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "VND", "Professional plan for growing teams", "Professional", true, "Pro", 299000m, 2990000m, 2, null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "VND", "Enterprise plan for large organizations", "Enterprise", true, "Enterprise", 999000m, 9990000m, 3, null }
                });

            migrationBuilder.InsertData(
                schema: "subscription",
                table: "PlanLimits",
                columns: new[] { "Id", "CreatedDateTime", "IsUnlimited", "LimitType", "LimitValue", "PlanId", "UpdatedDateTime" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 0, 1, new Guid("10000000-0000-0000-0000-000000000001"), null },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 1, 10, new Guid("10000000-0000-0000-0000-000000000001"), null },
                    { new Guid("20000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 2, 20, new Guid("10000000-0000-0000-0000-000000000001"), null },
                    { new Guid("20000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 3, 50, new Guid("10000000-0000-0000-0000-000000000001"), null },
                    { new Guid("20000000-0000-0000-0000-000000000005"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 4, 1, new Guid("10000000-0000-0000-0000-000000000001"), null },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 5, 7, new Guid("10000000-0000-0000-0000-000000000001"), null },
                    { new Guid("20000000-0000-0000-0000-000000000007"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 6, 10, new Guid("10000000-0000-0000-0000-000000000001"), null },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 7, 100, new Guid("10000000-0000-0000-0000-000000000001"), null },
                    { new Guid("20000000-0000-0000-0000-000000000011"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 0, 10, new Guid("10000000-0000-0000-0000-000000000002"), null },
                    { new Guid("20000000-0000-0000-0000-000000000012"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 1, 50, new Guid("10000000-0000-0000-0000-000000000002"), null },
                    { new Guid("20000000-0000-0000-0000-000000000013"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 2, 100, new Guid("10000000-0000-0000-0000-000000000002"), null },
                    { new Guid("20000000-0000-0000-0000-000000000014"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 3, 500, new Guid("10000000-0000-0000-0000-000000000002"), null },
                    { new Guid("20000000-0000-0000-0000-000000000015"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 4, 3, new Guid("10000000-0000-0000-0000-000000000002"), null },
                    { new Guid("20000000-0000-0000-0000-000000000016"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 5, 30, new Guid("10000000-0000-0000-0000-000000000002"), null },
                    { new Guid("20000000-0000-0000-0000-000000000017"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 6, 100, new Guid("10000000-0000-0000-0000-000000000002"), null },
                    { new Guid("20000000-0000-0000-0000-000000000018"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 7, 1000, new Guid("10000000-0000-0000-0000-000000000002"), null },
                    { new Guid("20000000-0000-0000-0000-000000000021"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, 0, null, new Guid("10000000-0000-0000-0000-000000000003"), null },
                    { new Guid("20000000-0000-0000-0000-000000000022"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, 1, null, new Guid("10000000-0000-0000-0000-000000000003"), null },
                    { new Guid("20000000-0000-0000-0000-000000000023"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, 2, null, new Guid("10000000-0000-0000-0000-000000000003"), null },
                    { new Guid("20000000-0000-0000-0000-000000000024"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, 3, null, new Guid("10000000-0000-0000-0000-000000000003"), null },
                    { new Guid("20000000-0000-0000-0000-000000000025"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 4, 10, new Guid("10000000-0000-0000-0000-000000000003"), null },
                    { new Guid("20000000-0000-0000-0000-000000000026"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), false, 5, 365, new Guid("10000000-0000-0000-0000-000000000003"), null },
                    { new Guid("20000000-0000-0000-0000-000000000027"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, 6, null, new Guid("10000000-0000-0000-0000-000000000003"), null },
                    { new Guid("20000000-0000-0000-0000-000000000028"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, 7, null, new Guid("10000000-0000-0000-0000-000000000003"), null }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000027"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000028"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                schema: "subscription",
                table: "SubscriptionPlans",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"));
        }
    }
}
