using ClassifiedAds.Modules.Identity.Authorization;
using System.Linq;

namespace ClassifiedAds.UnitTests.Identity;

/// <summary>
/// Verifies that all FE-18 SRS and traceability permissions are seeded in both
/// AdminPermissions and UserPermissions of RolePermissionMappings.
/// </summary>
public class SrsTraceabilityPermissionSeedTests
{
    private static readonly string[] ExpectedSrsPermissions =
    [
        "Permission:GetSrsDocuments",
        "Permission:AddSrsDocument",
        "Permission:DeleteSrsDocument",
        "Permission:TriggerSrsAnalysis",
        "Permission:ManageSrsRequirements",
        "Permission:GetSrsTraceability",
        "Permission:ManageTraceabilityLinks",
    ];

    [Fact]
    public void AdminPermissions_Should_ContainAllSrsTraceabilityPermissions()
    {
        foreach (var perm in ExpectedSrsPermissions)
        {
            RolePermissionMappings.AdminPermissions.Should().Contain(perm,
                $"Admin role must include SRS/traceability permission '{perm}' (FE-18)");
        }
    }

    [Fact]
    public void UserPermissions_Should_ContainAllSrsTraceabilityPermissions()
    {
        foreach (var perm in ExpectedSrsPermissions)
        {
            RolePermissionMappings.UserPermissions.Should().Contain(perm,
                $"User role must include SRS/traceability permission '{perm}' (FE-18)");
        }
    }

    [Fact]
    public void AdminPermissions_Should_HaveNoDuplicates()
    {
        RolePermissionMappings.AdminPermissions
            .Should().OnlyHaveUniqueItems("AdminPermissions must not have duplicate entries");
    }

    [Fact]
    public void UserPermissions_Should_HaveNoDuplicates()
    {
        RolePermissionMappings.UserPermissions
            .Should().OnlyHaveUniqueItems("UserPermissions must not have duplicate entries");
    }
}
