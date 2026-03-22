using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClassifiedAds.Migrator.Migrations.Notification
{
    /// <inheritdoc />
    public partial class OptimizeSmsPendingLookup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_Pending",
                schema: "notification",
                table: "SmsMessages",
                column: "NextAttemptDateTime",
                filter: "\"SentDateTime\" IS NULL")
                .Annotation("Npgsql:IndexInclude", new[] { "ExpiredDateTime", "AttemptCount", "MaxAttemptCount" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SmsMessages_Pending",
                schema: "notification",
                table: "SmsMessages");
        }
    }
}
