using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Subscription
{
    /// <inheritdoc />
    public partial class SubscriptionSeedSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotCurrency",
                schema: "subscription",
                table: "UserSubscriptions",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotPlanName",
                schema: "subscription",
                table: "UserSubscriptions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotPriceMonthly",
                schema: "subscription",
                table: "UserSubscriptions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SnapshotPriceYearly",
                schema: "subscription",
                table: "UserSubscriptions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotCurrency",
                schema: "subscription",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "SnapshotPlanName",
                schema: "subscription",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "SnapshotPriceMonthly",
                schema: "subscription",
                table: "UserSubscriptions");

            migrationBuilder.DropColumn(
                name: "SnapshotPriceYearly",
                schema: "subscription",
                table: "UserSubscriptions");
        }
    }
}
