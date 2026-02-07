using ClassifiedAds.Modules.Identity.Authorization;
using Xunit;
using FluentAssertions;
using System.Reflection;
using System.Linq;

namespace ClassifiedAds.UnitTests.Identity;

public class PermissionsTests
{
    [Fact]
    public void Permissions_Should_HaveConsistentNamingConvention()
    {
        // Arrange
        var permissionFields = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .ToList();

        // Act & Assert
        foreach (var field in permissionFields)
        {
            var value = (string?)field.GetValue(null);
            value.Should().NotBeNullOrEmpty($"Permission {field.Name} should have a value");
            value.Should().StartWith("Permission:", $"Permission {field.Name} should follow naming convention");
        }
    }

    [Fact]
    public void Permissions_Should_HaveUniqueValues()
    {
        // Arrange
        var permissionFields = typeof(Permissions)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => (string?)f.GetValue(null))
            .Where(v => v != null)
            .ToList();

        // Act & Assert
        permissionFields.Should().OnlyHaveUniqueItems("All permission values should be unique");
    }

    [Fact]
    public void Permissions_Should_ContainAllRequiredPermissions()
    {
        // Arrange
        var requiredPermissions = new[]
        {
            // Role permissions
            Permissions.GetRoles,
            Permissions.GetRole,
            Permissions.AddRole,
            Permissions.UpdateRole,
            Permissions.DeleteRole,

            // User permissions
            Permissions.GetUsers,
            Permissions.GetUser,
            Permissions.AddUser,
            Permissions.UpdateUser,
            Permissions.SetPassword,
            Permissions.DeleteUser,

            // User management actions
            Permissions.SendResetPasswordEmail,
            Permissions.SendConfirmationEmailAddressEmail,
            Permissions.AssignRole,
            Permissions.RemoveRole,
            Permissions.LockUser,
            Permissions.UnlockUser,
        };

        // Act & Assert
        foreach (var permission in requiredPermissions)
        {
            permission.Should().NotBeNullOrEmpty();
            permission.Should().StartWith("Permission:");
        }
    }

    [Fact]
    public void NewPermissions_Should_BeIncluded()
    {
        // Assert that new permissions are defined
        Permissions.AssignRole.Should().Be("Permission:AssignRole");
        Permissions.RemoveRole.Should().Be("Permission:RemoveRole");
        Permissions.LockUser.Should().Be("Permission:LockUser");
        Permissions.UnlockUser.Should().Be("Permission:UnlockUser");
    }
}
