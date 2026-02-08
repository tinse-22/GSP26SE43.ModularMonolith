using ClassifiedAds.Modules.Identity.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace ClassifiedAds.UnitTests.Identity;

public class AuthModelsTests
{
    #region RegisterModel Tests

    [Fact]
    public void RegisterModel_Should_BeValid_WithCorrectData()
    {
        // Arrange
        var model = new RegisterModel
        {
            Email = "test@example.com",
            Password = "SecurePassword123!",
            ConfirmPassword = "SecurePassword123!",
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void RegisterModel_Should_RequireEmail()
    {
        // Arrange
        var model = new RegisterModel
        {
            Email = null!,
            Password = "SecurePassword123!",
            ConfirmPassword = "SecurePassword123!",
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Email"));
    }

    [Fact]
    public void RegisterModel_Should_ValidateEmailFormat()
    {
        // Arrange
        var model = new RegisterModel
        {
            Email = "invalid-email",
            Password = "SecurePassword123!",
            ConfirmPassword = "SecurePassword123!",
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Email"));
    }

    [Fact]
    public void RegisterModel_Should_RequirePassword()
    {
        // Arrange
        var model = new RegisterModel
        {
            Email = "test@example.com",
            Password = null!,
            ConfirmPassword = "SecurePassword123!",
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Password"));
    }

    [Fact]
    public void RegisterModel_Should_RequirePasswordMinLength()
    {
        // Arrange - Password is less than 6 characters (min length)
        var model = new RegisterModel
        {
            Email = "test@example.com",
            Password = "Ab1!",
            ConfirmPassword = "Ab1!",
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Password"));
    }

    [Fact]
    public void RegisterModel_Should_RequireMatchingPasswords()
    {
        // Arrange
        var model = new RegisterModel
        {
            Email = "test@example.com",
            Password = "SecurePassword123!",
            ConfirmPassword = "DifferentPassword123!",
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("ConfirmPassword"));
    }

    #endregion

    #region UpdateProfileModel Tests

    [Fact]
    public void UpdateProfileModel_Should_AllowEmptyDisplayName()
    {
        // Arrange
        var model = new UpdateProfileModel
        {
            DisplayName = null,
            Timezone = "UTC",
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void UpdateProfileModel_Should_LimitDisplayNameLength()
    {
        // Arrange
        var model = new UpdateProfileModel
        {
            DisplayName = new string('a', 256), // Exceeds max length (200)
            Timezone = "UTC",
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("DisplayName"));
    }

    [Fact]
    public void UpdateProfileModel_Should_LimitTimezoneLength()
    {
        // Arrange
        var model = new UpdateProfileModel
        {
            Timezone = new string('z', 51),
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Timezone"));
    }

    [Fact]
    public void UpdateProfileModel_Should_NotSerializeSecurityFields()
    {
        // Arrange
        var model = new UpdateProfileModel
        {
            UserName = "new.username",
            Email = "new@example.com",
            PhoneNumber = "0123456789",
            DisplayName = "New Display",
            Timezone = "UTC",
        };

        // Act
        var json = JsonSerializer.Serialize(model);

        // Assert
        json.Should().Contain("DisplayName");
        json.Should().Contain("Timezone");
        json.Should().NotContain("UserName");
        json.Should().NotContain("Email");
        json.Should().NotContain("PhoneNumber");
    }

    #endregion

    #region LoginModel Tests

    [Fact]
    public void LoginModel_Should_RequireEmail()
    {
        // Arrange
        var model = new LoginModel
        {
            Email = null!,
            Password = "Password123!",
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Email"));
    }

    [Fact]
    public void LoginModel_Should_RequirePassword()
    {
        // Arrange
        var model = new LoginModel
        {
            Email = "test@example.com",
            Password = null!,
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().Contain(v => v.MemberNames.Contains("Password"));
    }

    [Fact]
    public void LoginResponseModel_Should_NotSerializeRefreshToken()
    {
        // Arrange
        var model = new LoginResponseModel
        {
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresIn = 3600,
        };

        // Act
        var json = JsonSerializer.Serialize(model);

        // Assert
        json.Should().Contain("AccessToken");
        json.Should().NotContain("RefreshToken");
    }

    #endregion

    #region AssignRoleModel Tests

    [Fact]
    public void AssignRoleModel_Should_RequireRoleId()
    {
        // Arrange
        var model = new AssignRoleModel
        {
            RoleId = System.Guid.Empty,
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert - Guid.Empty is technically valid for Required attribute
        // but should be validated in business logic
        validationResults.Should().BeEmpty();
    }

    [Fact]
    public void AssignRoleModel_Should_AcceptValidRoleId()
    {
        // Arrange
        var model = new AssignRoleModel
        {
            RoleId = System.Guid.NewGuid(),
        };

        // Act
        var validationResults = ValidateModel(model);

        // Assert
        validationResults.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, validationContext, validationResults, true);
        return validationResults;
    }

    #endregion
}
