namespace ClassifiedAds.Modules.Identity.Authorization;

/// <summary>
/// Single source of truth for default role-permission mappings.
/// </summary>
public static class RolePermissionMappings
{
    public static readonly string[] AdminPermissions =
    {
        // Identity
        Permissions.GetRoles,
        Permissions.GetRole,
        Permissions.AddRole,
        Permissions.UpdateRole,
        Permissions.DeleteRole,
        Permissions.GetUsers,
        Permissions.GetUser,
        Permissions.AddUser,
        Permissions.UpdateUser,
        Permissions.SetPassword,
        Permissions.DeleteUser,
        Permissions.SendResetPasswordEmail,
        Permissions.SendConfirmationEmailAddressEmail,
        Permissions.AssignRole,
        Permissions.RemoveRole,
        Permissions.LockUser,
        Permissions.UnlockUser,

        // Configuration
        "Permission:GetConfigurationEntries",
        "Permission:GetConfigurationEntry",
        "Permission:AddConfigurationEntry",
        "Permission:UpdateConfigurationEntry",
        "Permission:DeleteConfigurationEntry",
        "Permission:ExportConfigurationEntries",
        "Permission:ImportConfigurationEntries",

        // Audit log
        "Permission:GetAuditLogs",

        // Api documentation
        "Permission:GetProjects",
        "Permission:AddProject",
        "Permission:UpdateProject",
        "Permission:DeleteProject",
        "Permission:ArchiveProject",
        "Permission:GetSpecifications",
        "Permission:AddSpecification",
        "Permission:UpdateSpecification",
        "Permission:DeleteSpecification",
        "Permission:ActivateSpecification",
        "Permission:GetEndpoints",
        "Permission:AddEndpoint",
        "Permission:UpdateEndpoint",
        "Permission:DeleteEndpoint",

        // Storage
        "Permission:GetFiles",
        "Permission:UploadFile",
        "Permission:GetFile",
        "Permission:DownloadFile",
        "Permission:UpdateFile",
        "Permission:DeleteFile",
        "Permission:GetFileAuditLogs",

        // Test generation
        "Permission:GetTestSuites",
        "Permission:AddTestSuite",
        "Permission:UpdateTestSuite",
        "Permission:DeleteTestSuite",
        "Permission:ProposeTestOrder",
        "Permission:GetTestOrderProposal",
        "Permission:ReorderTestOrder",
        "Permission:ApproveTestOrder",
        "Permission:GenerateTestCases",
        "Permission:GetTestCases",
        "Permission:GenerateBoundaryNegativeTestCases",
        "Permission:AddTestCase",
        "Permission:UpdateTestCase",
        "Permission:DeleteTestCase",

        // SRS documents and traceability (FE-18)
        "Permission:GetSrsDocuments",
        "Permission:AddSrsDocument",
        "Permission:DeleteSrsDocument",
        "Permission:TriggerSrsAnalysis",
        "Permission:ManageSrsRequirements",
        "Permission:GetSrsTraceability",
        "Permission:ManageTraceabilityLinks",

        // Test execution and reporting
        "Permission:GetExecutionEnvironments",
        "Permission:AddExecutionEnvironment",
        "Permission:UpdateExecutionEnvironment",
        "Permission:DeleteExecutionEnvironment",
        "Permission:StartTestRun",
        "Permission:GetTestRuns",

        // Subscription and billing
        "Permission:GetPlans",
        "Permission:AddPlan",
        "Permission:UpdatePlan",
        "Permission:DeletePlan",
        "Permission:GetPlanAuditLogs",
        "Permission:GetSubscription",
        "Permission:GetCurrentSubscription",
        "Permission:AddSubscription",
        "Permission:UpdateSubscription",
        "Permission:CancelSubscription",
        "Permission:GetSubscriptionHistory",
        "Permission:GetPaymentTransactions",
        "Permission:AddPaymentTransaction",
        "Permission:GetUsageTracking",
        "Permission:UpdateUsageTracking",
        "Permission:CreateSubscriptionPayment",
        "Permission:GetPaymentIntent",
        "Permission:CreatePayOsCheckout",
        "Permission:SyncPayment",
    };

    public static readonly string[] UserPermissions =
    {
        // Api documentation
        "Permission:GetProjects",
        "Permission:AddProject",
        "Permission:UpdateProject",
        "Permission:DeleteProject",
        "Permission:ArchiveProject",
        "Permission:GetSpecifications",
        "Permission:AddSpecification",
        "Permission:UpdateSpecification",
        "Permission:DeleteSpecification",
        "Permission:ActivateSpecification",
        "Permission:GetEndpoints",
        "Permission:AddEndpoint",
        "Permission:UpdateEndpoint",
        "Permission:DeleteEndpoint",

        // Test generation
        "Permission:GetTestSuites",
        "Permission:AddTestSuite",
        "Permission:UpdateTestSuite",
        "Permission:DeleteTestSuite",
        "Permission:ProposeTestOrder",
        "Permission:GetTestOrderProposal",
        "Permission:ReorderTestOrder",
        "Permission:ApproveTestOrder",
        "Permission:GenerateTestCases",
        "Permission:GetTestCases",
        "Permission:GenerateBoundaryNegativeTestCases",
        "Permission:AddTestCase",
        "Permission:UpdateTestCase",
        "Permission:DeleteTestCase",

        // SRS documents and traceability (FE-18)
        "Permission:GetSrsDocuments",
        "Permission:AddSrsDocument",
        "Permission:DeleteSrsDocument",
        "Permission:TriggerSrsAnalysis",
        "Permission:ManageSrsRequirements",
        "Permission:GetSrsTraceability",
        "Permission:ManageTraceabilityLinks",

        // Test execution and reporting
        "Permission:GetExecutionEnvironments",
        "Permission:AddExecutionEnvironment",
        "Permission:UpdateExecutionEnvironment",
        "Permission:DeleteExecutionEnvironment",
        "Permission:StartTestRun",
        "Permission:GetTestRuns",

        // Storage (owner-scoped)
        "Permission:GetFiles",
        "Permission:UploadFile",
        "Permission:GetFile",
        "Permission:DownloadFile",
        "Permission:UpdateFile",
        "Permission:DeleteFile",
        "Permission:GetFileAuditLogs",

        // Subscription self-service
        "Permission:GetPlans",
        "Permission:GetSubscription",
        "Permission:GetCurrentSubscription",
        "Permission:CancelSubscription",
        "Permission:GetSubscriptionHistory",
        "Permission:GetPaymentTransactions",
        "Permission:GetUsageTracking",
        "Permission:CreateSubscriptionPayment",
        "Permission:GetPaymentIntent",
        "Permission:CreatePayOsCheckout",
    };
}
