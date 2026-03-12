namespace ClassifiedAds.Modules.TestGeneration.Authorization;

public static class Permissions
{
    // Test Suite Scope (FE-04-01)
    public const string GetTestSuites = "Permission:GetTestSuites";
    public const string AddTestSuite = "Permission:AddTestSuite";
    public const string UpdateTestSuite = "Permission:UpdateTestSuite";
    public const string DeleteTestSuite = "Permission:DeleteTestSuite";

    // Test Order (FE-05A)
    public const string ProposeTestOrder = "Permission:ProposeTestOrder";
    public const string GetTestOrderProposal = "Permission:GetTestOrderProposal";
    public const string ReorderTestOrder = "Permission:ReorderTestOrder";
    public const string ApproveTestOrder = "Permission:ApproveTestOrder";

    // Test Case Generation (FE-05B)
    public const string GenerateTestCases = "Permission:GenerateTestCases";
    public const string GetTestCases = "Permission:GetTestCases";

    // Boundary/Negative Test Case Generation (FE-06)
    public const string GenerateBoundaryNegativeTestCases = "Permission:GenerateBoundaryNegativeTestCases";

    // Test Case CRUD (FE-06 CRUD)
    public const string AddTestCase = "Permission:AddTestCase";
    public const string UpdateTestCase = "Permission:UpdateTestCase";
    public const string DeleteTestCase = "Permission:DeleteTestCase";
}
