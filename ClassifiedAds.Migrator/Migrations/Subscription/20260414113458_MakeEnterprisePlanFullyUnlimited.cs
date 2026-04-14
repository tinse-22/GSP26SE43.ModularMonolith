using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Subscription
{
    /// <inheritdoc />
    public partial class MakeEnterprisePlanFullyUnlimited : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000025"),
                columns: new[] { "IsUnlimited", "LimitValue" },
                values: new object[] { true, null });

            migrationBuilder.UpdateData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000026"),
                columns: new[] { "IsUnlimited", "LimitValue" },
                values: new object[] { true, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000025"),
                columns: new[] { "IsUnlimited", "LimitValue" },
                values: new object[] { false, 10 });

            migrationBuilder.UpdateData(
                schema: "subscription",
                table: "PlanLimits",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000026"),
                columns: new[] { "IsUnlimited", "LimitValue" },
                values: new object[] { false, 365 });
        }
    }
}
