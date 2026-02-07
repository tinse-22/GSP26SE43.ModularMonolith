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
    [InlineData("Permission:GetSubscription")]
    [InlineData("Permission:GetCurrentSubscription")]
    [InlineData("Permission:AddSubscription")]
    [InlineData("Permission:UpdateSubscription")]
    [InlineData("Permission:CancelSubscription")]
    [InlineData("Permission:GetSubscriptionHistory")]
    [InlineData("Permission:GetPaymentTransactions")]
    [InlineData("Permission:AddPaymentTransaction")]
    [InlineData("Permission:GetUsageTracking")]
    [InlineData("Permission:UpdateUsageTracking")]
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
        fields.Should().HaveCount(15, "Subscription module should have exactly 15 permissions");
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

    [Fact]
    public void GetSubscription_Should_HaveCorrectValue()
    {
        Permissions.GetSubscription.Should().Be("Permission:GetSubscription");
    }

    [Fact]
    public void GetCurrentSubscription_Should_HaveCorrectValue()
    {
        Permissions.GetCurrentSubscription.Should().Be("Permission:GetCurrentSubscription");
    }

    [Fact]
    public void AddSubscription_Should_HaveCorrectValue()
    {
        Permissions.AddSubscription.Should().Be("Permission:AddSubscription");
    }

    [Fact]
    public void UpdateSubscription_Should_HaveCorrectValue()
    {
        Permissions.UpdateSubscription.Should().Be("Permission:UpdateSubscription");
    }

    [Fact]
    public void CancelSubscription_Should_HaveCorrectValue()
    {
        Permissions.CancelSubscription.Should().Be("Permission:CancelSubscription");
    }

    [Fact]
    public void GetSubscriptionHistory_Should_HaveCorrectValue()
    {
        Permissions.GetSubscriptionHistory.Should().Be("Permission:GetSubscriptionHistory");
    }

    [Fact]
    public void GetPaymentTransactions_Should_HaveCorrectValue()
    {
        Permissions.GetPaymentTransactions.Should().Be("Permission:GetPaymentTransactions");
    }

    [Fact]
    public void AddPaymentTransaction_Should_HaveCorrectValue()
    {
        Permissions.AddPaymentTransaction.Should().Be("Permission:AddPaymentTransaction");
    }

    [Fact]
    public void GetUsageTracking_Should_HaveCorrectValue()
    {
        Permissions.GetUsageTracking.Should().Be("Permission:GetUsageTracking");
    }

    [Fact]
    public void UpdateUsageTracking_Should_HaveCorrectValue()
    {
        Permissions.UpdateUsageTracking.Should().Be("Permission:UpdateUsageTracking");
    }

    #endregion
}
