using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Subscription
{
    /// <inheritdoc />
    public partial class PaymentFeature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "subscription",
                table: "PaymentTransactions",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2);

            migrationBuilder.AddColumn<Guid>(
                name: "PaymentIntentId",
                schema: "subscription",
                table: "PaymentTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Provider",
                schema: "subscription",
                table: "PaymentTransactions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderRef",
                schema: "subscription",
                table: "PaymentTransactions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentIntents",
                schema: "subscription",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    BillingCycle = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CheckoutUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OrderCode = table.Column<long>(type: "bigint", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    CreatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedDateTime = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentIntents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentIntents_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalSchema: "subscription",
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentIntents_UserSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalSchema: "subscription",
                        principalTable: "UserSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_PaymentIntentId",
                schema: "subscription",
                table: "PaymentTransactions",
                column: "PaymentIntentId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Provider_ProviderRef",
                schema: "subscription",
                table: "PaymentTransactions",
                columns: new[] { "Provider", "ProviderRef" },
                unique: true,
                filter: "\"ProviderRef\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_OrderCode",
                schema: "subscription",
                table: "PaymentIntents",
                column: "OrderCode",
                unique: true,
                filter: "\"OrderCode\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_PlanId",
                schema: "subscription",
                table: "PaymentIntents",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_Status",
                schema: "subscription",
                table: "PaymentIntents",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_Status_CreatedDateTime",
                schema: "subscription",
                table: "PaymentIntents",
                columns: new[] { "Status", "CreatedDateTime" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_Status_Purpose",
                schema: "subscription",
                table: "PaymentIntents",
                columns: new[] { "Status", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_SubscriptionId",
                schema: "subscription",
                table: "PaymentIntents",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentIntents_UserId",
                schema: "subscription",
                table: "PaymentIntents",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_PaymentIntents_PaymentIntentId",
                schema: "subscription",
                table: "PaymentTransactions",
                column: "PaymentIntentId",
                principalSchema: "subscription",
                principalTable: "PaymentIntents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_PaymentIntents_PaymentIntentId",
                schema: "subscription",
                table: "PaymentTransactions");

            migrationBuilder.DropTable(
                name: "PaymentIntents",
                schema: "subscription");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_PaymentIntentId",
                schema: "subscription",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_Provider_ProviderRef",
                schema: "subscription",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "PaymentIntentId",
                schema: "subscription",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "Provider",
                schema: "subscription",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "ProviderRef",
                schema: "subscription",
                table: "PaymentTransactions");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                schema: "subscription",
                table: "PaymentTransactions",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);
        }
    }
}
