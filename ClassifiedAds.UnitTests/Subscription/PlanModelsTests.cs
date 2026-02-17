using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Modules.Subscription.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ClassifiedAds.UnitTests.Subscription;

public class PlanModelsTests
{
    #region CreateUpdatePlanModel Tests

    [Fact]
    public void CreateUpdatePlanModel_Should_BeValid_WithCorrectData()
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = "Pro Plan",
            Description = "Professional plan with advanced features",
            PriceMonthly = 29.99m,
            PriceYearly = 299.99m,
            Currency = "USD",
            IsActive = true,
            SortOrder = 1,
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void CreateUpdatePlanModel_Should_RequireName()
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = null!,
            DisplayName = "Pro Plan",
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(v => v.MemberNames.Contains("Name"));
    }

    [Fact]
    public void CreateUpdatePlanModel_Should_RequireDisplayName()
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = null!,
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(v => v.MemberNames.Contains("DisplayName"));
    }

    [Fact]
    public void CreateUpdatePlanModel_Should_EnforceNameMaxLength()
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = new string('A', 51),
            DisplayName = "Pro Plan",
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(v => v.MemberNames.Contains("Name"));
    }

    [Fact]
    public void CreateUpdatePlanModel_Should_EnforceDisplayNameMaxLength()
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = new string('A', 101),
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(v => v.MemberNames.Contains("DisplayName"));
    }

    [Fact]
    public void CreateUpdatePlanModel_Should_EnforceDescriptionMaxLength()
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = "Pro Plan",
            Description = new string('A', 501),
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(v => v.MemberNames.Contains("Description"));
    }

    [Fact]
    public void CreateUpdatePlanModel_Should_AcceptNullPrices()
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = "Free",
            DisplayName = "Free Plan",
            PriceMonthly = null,
            PriceYearly = null,
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().BeEmpty();
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("VND")]
    [InlineData("EUR")]
    public void CreateUpdatePlanModel_Should_AcceptValidCurrencies(string currency)
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = "Pro Plan",
            Currency = currency,
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().NotContain(v => v.MemberNames.Contains("Currency"));
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("12")]
    [InlineData("U1D")]
    public void CreateUpdatePlanModel_Should_RejectInvalidCurrencies(string currency)
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = "Pro Plan",
            Currency = currency,
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(v => v.MemberNames.Contains("Currency"));
    }

    [Fact]
    public void CreateUpdatePlanModel_Should_DefaultCurrencyToUSD()
    {
        // Arrange & Act
        var model = new CreateUpdatePlanModel();

        // Assert
        model.Currency.Should().Be("USD");
    }

    [Fact]
    public void CreateUpdatePlanModel_Should_DefaultIsActiveToTrue()
    {
        // Arrange & Act
        var model = new CreateUpdatePlanModel();

        // Assert
        model.IsActive.Should().BeTrue();
    }

    [Fact]
    public void CreateUpdatePlanModel_Should_DefaultLimitsToEmptyList()
    {
        // Arrange & Act
        var model = new CreateUpdatePlanModel();

        // Assert
        model.Limits.Should().NotBeNull();
        model.Limits.Should().BeEmpty();
    }

    [Fact]
    public void CreateUpdatePlanModel_Should_RejectNegativeSortOrder()
    {
        // Arrange
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = "Pro Plan",
            SortOrder = -1,
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(v => v.MemberNames.Contains("SortOrder"));
    }

    #endregion

    #region PlanLimitModel Tests

    [Fact]
    public void PlanLimitModel_Should_BeValid_WithCorrectData()
    {
        // Arrange
        var model = new PlanLimitModel
        {
            LimitType = LimitType.MaxProjects,
            LimitValue = 10,
            IsUnlimited = false,
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().BeEmpty();
    }

    [Fact]
    public void PlanLimitModel_Should_RequireLimitType()
    {
        // Arrange
        var model = new PlanLimitModel
        {
            LimitType = null!,
            LimitValue = 10,
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(v => v.MemberNames.Contains("LimitType"));
    }

    [Fact]
    public void PlanLimitModel_Should_BeValid_WhenUnlimited()
    {
        // Arrange
        var model = new PlanLimitModel
        {
            LimitType = LimitType.MaxProjects,
            LimitValue = null,
            IsUnlimited = true,
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region PlanModel Tests

    [Fact]
    public void PlanModel_Should_DefaultLimitsToEmptyList()
    {
        // Arrange & Act
        var model = new PlanModel();

        // Assert
        model.Limits.Should().NotBeNull();
        model.Limits.Should().BeEmpty();
    }

    #endregion

    #region Helpers

    private static List<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, context, validationResults, true);
        return validationResults;
    }

    #endregion
}
