using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClassifiedAds.Migrator.Migrations.Identity
{
    /// <inheritdoc />
    public partial class UpdateRolePermissionMatrix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                schema: "identity",
                table: "RoleClaims",
                columns: new[] { "Id", "CreatedDateTime", "RoleId", "Type", "UpdatedDateTime", "Value" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000018"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetConfigurationEntries" },
                    { new Guid("00000000-0000-0000-0001-000000000019"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetConfigurationEntry" },
                    { new Guid("00000000-0000-0000-0001-000000000020"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddConfigurationEntry" },
                    { new Guid("00000000-0000-0000-0001-000000000021"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateConfigurationEntry" },
                    { new Guid("00000000-0000-0000-0001-000000000022"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:DeleteConfigurationEntry" },
                    { new Guid("00000000-0000-0000-0001-000000000023"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:ExportConfigurationEntries" },
                    { new Guid("00000000-0000-0000-0001-000000000024"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:ImportConfigurationEntries" },
                    { new Guid("00000000-0000-0000-0001-000000000025"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetAuditLogs" },
                    { new Guid("00000000-0000-0000-0001-000000000026"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetProjects" },
                    { new Guid("00000000-0000-0000-0001-000000000027"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddProject" },
                    { new Guid("00000000-0000-0000-0001-000000000028"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateProject" },
                    { new Guid("00000000-0000-0000-0001-000000000029"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:DeleteProject" },
                    { new Guid("00000000-0000-0000-0001-000000000030"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:ArchiveProject" },
                    { new Guid("00000000-0000-0000-0001-000000000031"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetSpecifications" },
                    { new Guid("00000000-0000-0000-0001-000000000032"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddSpecification" },
                    { new Guid("00000000-0000-0000-0001-000000000033"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateSpecification" },
                    { new Guid("00000000-0000-0000-0001-000000000034"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:DeleteSpecification" },
                    { new Guid("00000000-0000-0000-0001-000000000035"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:ActivateSpecification" },
                    { new Guid("00000000-0000-0000-0001-000000000036"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetEndpoints" },
                    { new Guid("00000000-0000-0000-0001-000000000037"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddEndpoint" },
                    { new Guid("00000000-0000-0000-0001-000000000038"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateEndpoint" },
                    { new Guid("00000000-0000-0000-0001-000000000039"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:DeleteEndpoint" },
                    { new Guid("00000000-0000-0000-0001-000000000040"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetFiles" },
                    { new Guid("00000000-0000-0000-0001-000000000041"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UploadFile" },
                    { new Guid("00000000-0000-0000-0001-000000000042"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetFile" },
                    { new Guid("00000000-0000-0000-0001-000000000043"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:DownloadFile" },
                    { new Guid("00000000-0000-0000-0001-000000000044"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateFile" },
                    { new Guid("00000000-0000-0000-0001-000000000045"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:DeleteFile" },
                    { new Guid("00000000-0000-0000-0001-000000000046"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetFileAuditLogs" },
                    { new Guid("00000000-0000-0000-0001-000000000047"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetTestSuites" },
                    { new Guid("00000000-0000-0000-0001-000000000048"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddTestSuite" },
                    { new Guid("00000000-0000-0000-0001-000000000049"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateTestSuite" },
                    { new Guid("00000000-0000-0000-0001-000000000050"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:DeleteTestSuite" },
                    { new Guid("00000000-0000-0000-0001-000000000051"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:ProposeTestOrder" },
                    { new Guid("00000000-0000-0000-0001-000000000052"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetTestOrderProposal" },
                    { new Guid("00000000-0000-0000-0001-000000000053"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:ReorderTestOrder" },
                    { new Guid("00000000-0000-0000-0001-000000000054"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:ApproveTestOrder" },
                    { new Guid("00000000-0000-0000-0001-000000000055"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GenerateTestCases" },
                    { new Guid("00000000-0000-0000-0001-000000000056"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetTestCases" },
                    { new Guid("00000000-0000-0000-0001-000000000057"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GenerateBoundaryNegativeTestCases" },
                    { new Guid("00000000-0000-0000-0001-000000000058"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddTestCase" },
                    { new Guid("00000000-0000-0000-0001-000000000059"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateTestCase" },
                    { new Guid("00000000-0000-0000-0001-000000000060"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:DeleteTestCase" },
                    { new Guid("00000000-0000-0000-0001-000000000061"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetExecutionEnvironments" },
                    { new Guid("00000000-0000-0000-0001-000000000062"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddExecutionEnvironment" },
                    { new Guid("00000000-0000-0000-0001-000000000063"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateExecutionEnvironment" },
                    { new Guid("00000000-0000-0000-0001-000000000064"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:DeleteExecutionEnvironment" },
                    { new Guid("00000000-0000-0000-0001-000000000065"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:StartTestRun" },
                    { new Guid("00000000-0000-0000-0001-000000000066"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetTestRuns" },
                    { new Guid("00000000-0000-0000-0001-000000000067"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetPlans" },
                    { new Guid("00000000-0000-0000-0001-000000000068"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddPlan" },
                    { new Guid("00000000-0000-0000-0001-000000000069"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdatePlan" },
                    { new Guid("00000000-0000-0000-0001-000000000070"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:DeletePlan" },
                    { new Guid("00000000-0000-0000-0001-000000000071"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetPlanAuditLogs" },
                    { new Guid("00000000-0000-0000-0001-000000000072"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetSubscription" },
                    { new Guid("00000000-0000-0000-0001-000000000073"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetCurrentSubscription" },
                    { new Guid("00000000-0000-0000-0001-000000000074"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddSubscription" },
                    { new Guid("00000000-0000-0000-0001-000000000075"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateSubscription" },
                    { new Guid("00000000-0000-0000-0001-000000000076"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:CancelSubscription" },
                    { new Guid("00000000-0000-0000-0001-000000000077"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetSubscriptionHistory" },
                    { new Guid("00000000-0000-0000-0001-000000000078"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetPaymentTransactions" },
                    { new Guid("00000000-0000-0000-0001-000000000079"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:AddPaymentTransaction" },
                    { new Guid("00000000-0000-0000-0001-000000000080"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetUsageTracking" },
                    { new Guid("00000000-0000-0000-0001-000000000081"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:UpdateUsageTracking" },
                    { new Guid("00000000-0000-0000-0001-000000000082"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:CreateSubscriptionPayment" },
                    { new Guid("00000000-0000-0000-0001-000000000083"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:GetPaymentIntent" },
                    { new Guid("00000000-0000-0000-0001-000000000084"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:CreatePayOsCheckout" },
                    { new Guid("00000000-0000-0000-0001-000000000085"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000001"), "Permission", null, "Permission:SyncPayment" },
                    { new Guid("00000000-0000-0000-0002-000000000001"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetProjects" },
                    { new Guid("00000000-0000-0000-0002-000000000002"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:AddProject" },
                    { new Guid("00000000-0000-0000-0002-000000000003"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:UpdateProject" },
                    { new Guid("00000000-0000-0000-0002-000000000004"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:DeleteProject" },
                    { new Guid("00000000-0000-0000-0002-000000000005"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:ArchiveProject" },
                    { new Guid("00000000-0000-0000-0002-000000000006"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetSpecifications" },
                    { new Guid("00000000-0000-0000-0002-000000000007"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:AddSpecification" },
                    { new Guid("00000000-0000-0000-0002-000000000008"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:UpdateSpecification" },
                    { new Guid("00000000-0000-0000-0002-000000000009"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:DeleteSpecification" },
                    { new Guid("00000000-0000-0000-0002-000000000010"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:ActivateSpecification" },
                    { new Guid("00000000-0000-0000-0002-000000000011"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetEndpoints" },
                    { new Guid("00000000-0000-0000-0002-000000000012"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:AddEndpoint" },
                    { new Guid("00000000-0000-0000-0002-000000000013"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:UpdateEndpoint" },
                    { new Guid("00000000-0000-0000-0002-000000000014"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:DeleteEndpoint" },
                    { new Guid("00000000-0000-0000-0002-000000000015"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetTestSuites" },
                    { new Guid("00000000-0000-0000-0002-000000000016"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:AddTestSuite" },
                    { new Guid("00000000-0000-0000-0002-000000000017"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:UpdateTestSuite" },
                    { new Guid("00000000-0000-0000-0002-000000000018"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:DeleteTestSuite" },
                    { new Guid("00000000-0000-0000-0002-000000000019"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:ProposeTestOrder" },
                    { new Guid("00000000-0000-0000-0002-000000000020"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetTestOrderProposal" },
                    { new Guid("00000000-0000-0000-0002-000000000021"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:ReorderTestOrder" },
                    { new Guid("00000000-0000-0000-0002-000000000022"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:ApproveTestOrder" },
                    { new Guid("00000000-0000-0000-0002-000000000023"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GenerateTestCases" },
                    { new Guid("00000000-0000-0000-0002-000000000024"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetTestCases" },
                    { new Guid("00000000-0000-0000-0002-000000000025"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GenerateBoundaryNegativeTestCases" },
                    { new Guid("00000000-0000-0000-0002-000000000026"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:AddTestCase" },
                    { new Guid("00000000-0000-0000-0002-000000000027"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:UpdateTestCase" },
                    { new Guid("00000000-0000-0000-0002-000000000028"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:DeleteTestCase" },
                    { new Guid("00000000-0000-0000-0002-000000000029"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetExecutionEnvironments" },
                    { new Guid("00000000-0000-0000-0002-000000000030"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:AddExecutionEnvironment" },
                    { new Guid("00000000-0000-0000-0002-000000000031"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:UpdateExecutionEnvironment" },
                    { new Guid("00000000-0000-0000-0002-000000000032"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:DeleteExecutionEnvironment" },
                    { new Guid("00000000-0000-0000-0002-000000000033"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:StartTestRun" },
                    { new Guid("00000000-0000-0000-0002-000000000034"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetTestRuns" },
                    { new Guid("00000000-0000-0000-0002-000000000035"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetPlans" },
                    { new Guid("00000000-0000-0000-0002-000000000036"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetSubscription" },
                    { new Guid("00000000-0000-0000-0002-000000000037"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetCurrentSubscription" },
                    { new Guid("00000000-0000-0000-0002-000000000038"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:CancelSubscription" },
                    { new Guid("00000000-0000-0000-0002-000000000039"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetSubscriptionHistory" },
                    { new Guid("00000000-0000-0000-0002-000000000040"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetPaymentTransactions" },
                    { new Guid("00000000-0000-0000-0002-000000000041"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetUsageTracking" },
                    { new Guid("00000000-0000-0000-0002-000000000042"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:CreateSubscriptionPayment" },
                    { new Guid("00000000-0000-0000-0002-000000000043"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:GetPaymentIntent" },
                    { new Guid("00000000-0000-0000-0002-000000000044"), new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("00000000-0000-0000-0000-000000000002"), "Permission", null, "Permission:CreatePayOsCheckout" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000018"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000019"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000020"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000021"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000022"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000023"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000024"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000025"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000026"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000027"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000028"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000029"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000030"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000031"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000032"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000033"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000034"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000035"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000036"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000037"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000038"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000039"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000040"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000041"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000042"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000043"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000044"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000045"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000046"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000047"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000048"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000049"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000050"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000051"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000052"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000053"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000054"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000055"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000056"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000057"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000058"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000059"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000060"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000061"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000062"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000063"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000064"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000065"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000066"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000067"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000068"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000069"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000070"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000071"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000072"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000073"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000074"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000075"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000076"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000077"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000078"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000079"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000080"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000081"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000082"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000083"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000084"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0001-000000000085"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000001"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000002"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000003"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000004"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000005"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000006"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000007"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000008"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000009"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000010"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000011"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000012"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000013"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000014"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000015"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000016"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000017"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000018"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000019"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000020"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000021"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000022"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000023"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000024"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000025"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000026"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000027"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000028"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000029"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000030"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000031"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000032"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000033"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000034"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000035"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000036"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000037"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000038"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000039"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000040"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000041"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000042"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000043"));

            migrationBuilder.DeleteData(
                schema: "identity",
                table: "RoleClaims",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0002-000000000044"));
        }
    }
}
