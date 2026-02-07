using ClassifiedAds.Modules.Subscription.Authorization;
using System.Linq;
using System.Reflection;

namespace ClassifiedAds.UnitTests.Subscription;

public class PermissionsTests
{
    #region Naming Convention Tests

    [Fact]
    public void AllPermissions_Should_StartWithPermissionPrefix()
    {
        // Arrange
        var fields = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .ToList();

        // Assert
        foreach (var field in fields)
        {
            var value = (string)field.GetValue(null)!;
            value.Should().StartWith("Permission:", $"'{field.Name}' should follow the 'Permission:' prefix convention");
        }
    }

    [Fact]
    public void AllPermissions_Should_HaveUniqueValues()
    {
        // Arrange
        var fields = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        // Assert
        fields.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void AllPermissions_Should_NotBeNullOrEmpty()
    {
        // Arrange
        var fields = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .ToList();

        // Assert
        foreach (var field in fields)
        {
            var value = (string)field.GetValue(null)!;
            value.Should().NotBeNullOrWhiteSpace($"Permission constant '{field.Name}' should not be null or empty");
        }
    }

    #endregion

    #region Required Permissions Tests

    [Theory]
    [InlineData("Permission:GetPlans")]
    [InlineData("Permission:AddPlan")]
    [InlineData("Permission:UpdatePlan")]
    [InlineData("Permission:DeletePlan")]
    [InlineData("Permission:GetPlanAuditLogs")]
    public void Permissions_Should_ContainRequiredValues(string expectedPermission)
    {
        // Arrange
        var fields = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        // Assert
        fields.Should().Contain(expectedPermission);
    }

    [Fact]
    public void Permissions_Should_HaveExpectedCount()
    {
        // Arrange
        var fields = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .ToList();

        // Assert
        fields.Should().HaveCount(5, "Subscription module should have exactly 5 permissions");
    }

    #endregion

    #region Constant Value Tests

    [Fact]
    public void GetPlans_Should_HaveCorrectValue()
    {
        Permissions.GetPlans.Should().Be("Permission:GetPlans");
    }

    [Fact]
    public void AddPlan_Should_HaveCorrectValue()
    {
        Permissions.AddPlan.Should().Be("Permission:AddPlan");
    }

    [Fact]
    public void UpdatePlan_Should_HaveCorrectValue()
    {
        Permissions.UpdatePlan.Should().Be("Permission:UpdatePlan");
    }

    [Fact]
    public void DeletePlan_Should_HaveCorrectValue()
    {
        Permissions.DeletePlan.Should().Be("Permission:DeletePlan");
    }

    [Fact]
    public void GetPlanAuditLogs_Should_HaveCorrectValue()
    {
        Permissions.GetPlanAuditLogs.Should().Be("Permission:GetPlanAuditLogs");
    }

    #endregion
}
