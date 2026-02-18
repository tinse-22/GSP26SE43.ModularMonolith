using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class PathParameterTemplateServiceTests
{
    private readonly PathParameterTemplateService _service = new();

    [Fact]
    public void ExtractPathParameters_MultiplePlaceholders_ReturnsOrderedList()
    {
        // Act
        var result = _service.ExtractPathParameters("/api/users/{userId}/orders/{orderId}");

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("userId");
        result[0].Position.Should().Be(0);
        result[1].Name.Should().Be("orderId");
        result[1].Position.Should().Be(1);
    }

    [Fact]
    public void ExtractPathParameters_DuplicatePlaceholder_ShouldThrowValidationException()
    {
        // Act
        var act = () => _service.ExtractPathParameters("/api/{id}/sub/{id}");

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*trùng lặp*");
    }

    [Fact]
    public void ExtractPathParameters_InvalidPlaceholderName_ShouldThrowValidationException()
    {
        // Act
        var act = () => _service.ExtractPathParameters("/api/users/{user-id}");

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*không hợp lệ*");
    }

    [Fact]
    public void EnsurePathParameterConsistency_MissingPathParam_ShouldAutoCreateAndForceRequired()
    {
        // Arrange
        var parameters = new List<ManualParameterDefinition>
        {
            new()
            {
                Name = "userId",
                Location = "Path",
                DataType = EndpointParameterDataType.Integer,
                IsRequired = false,
            },
        };

        // Act
        var result = _service.EnsurePathParameterConsistency(
            "/api/users/{userId}/orders/{orderId}",
            parameters);

        // Assert
        result.Should().HaveCount(2);
        result.Should().ContainSingle(p =>
            p.Name == "userId" &&
            p.Location == "Path" &&
            p.DataType == EndpointParameterDataType.Integer &&
            p.IsRequired);
        result.Should().ContainSingle(p =>
            p.Name == "orderId" &&
            p.Location == "Path" &&
            p.DataType == EndpointParameterDataType.String &&
            p.IsRequired);
    }

    [Fact]
    public void EnsurePathParameterConsistency_ExtraPathParam_ShouldThrowValidationException()
    {
        // Arrange
        var parameters = new List<ManualParameterDefinition>
        {
            new() { Name = "wrongName", Location = "Path", IsRequired = true },
        };

        // Act
        var act = () => _service.EnsurePathParameterConsistency(
            "/api/users/{userId}",
            parameters);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*không tồn tại trong path*");
    }

    [Fact]
    public void ResolveUrl_CaseInsensitiveAndEncodedValues_ShouldResolveSuccessfully()
    {
        // Arrange
        var values = new Dictionary<string, string>
        {
            ["USERID"] = "42",
            ["QUERY"] = "hello world/test",
        };

        // Act
        var result = _service.ResolveUrl("/api/users/{userId}/search/{query}", values);

        // Assert
        result.IsFullyResolved.Should().BeTrue();
        result.ResolvedUrl.Should().Be("/api/users/42/search/hello%20world%2Ftest");
        result.UnresolvedParameters.Should().BeEmpty();
        result.ResolvedParameters.Should().ContainKey("userId").WhoseValue.Should().Be("42");
        result.ResolvedParameters.Should().ContainKey("query").WhoseValue.Should().Be("hello world/test");
    }

    [Fact]
    public void ResolveUrl_MissingOrEmptyValues_ShouldReturnUnresolvedResult()
    {
        // Arrange
        var values = new Dictionary<string, string>
        {
            ["userId"] = string.Empty,
        };

        // Act
        var result = _service.ResolveUrl("/api/users/{userId}/orders/{orderId}", values);

        // Assert
        result.IsFullyResolved.Should().BeFalse();
        result.ResolvedUrl.Should().BeNull();
        result.UnresolvedParameters.Should().Contain(new[] { "userId", "orderId" });
    }

    [Fact]
    public void ResolveUrl_StaticPath_ShouldReturnPathAsResolved()
    {
        // Act
        var result = _service.ResolveUrl("/api/health", null!);

        // Assert
        result.IsFullyResolved.Should().BeTrue();
        result.ResolvedUrl.Should().Be("/api/health");
        result.UnresolvedParameters.Should().BeEmpty();
    }

    [Fact]
    public void GenerateMutations_IntegerWithoutFormat_ShouldContainMaxInt64Mutation()
    {
        // Act
        var mutations = _service.GenerateMutations("userId", "integer", null, "42");

        // Assert
        mutations.Should().Contain(m =>
            m.MutationType == "boundary_max_int64" &&
            m.Value == "9223372036854775807" &&
            m.ExpectedStatusCode == 200);
        mutations.Should().Contain(m =>
            m.MutationType == "nonExistent" &&
            m.Value == "999999999" &&
            m.ExpectedStatusCode == 404);
    }

    [Fact]
    public void GenerateMutations_IntegerInt32_ShouldContainMaxAndOverflowMutations()
    {
        // Act
        var mutations = _service.GenerateMutations("id", "integer", "int32", "1");

        // Assert
        mutations.Should().Contain(m =>
            m.MutationType == "boundary_max_int32" &&
            m.Value == "2147483647" &&
            m.ExpectedStatusCode == 200);
        mutations.Should().Contain(m =>
            m.MutationType == "boundary_overflow_int32" &&
            m.Value == "2147483648" &&
            m.ExpectedStatusCode == 400);
    }

    [Fact]
    public void GenerateMutations_UuidString_ShouldContainUuidSpecificMutations()
    {
        // Act
        var mutations = _service.GenerateMutations("id", "string", "uuid", Guid.NewGuid().ToString());

        // Assert
        mutations.Should().Contain(m => m.MutationType == "invalidFormat" && m.Value == "not-a-uuid");
        mutations.Should().Contain(m => m.MutationType == "partialUuid" && m.Value == "550e8400-e29b-41d4");
        mutations.Should().Contain(m =>
            m.MutationType == "allZerosUuid" &&
            m.Value == "00000000-0000-0000-0000-000000000000" &&
            m.ExpectedStatusCode == 404);
    }

    [Fact]
    public void GenerateMutations_Number_ShouldContainVerySmallMutationWithExpectedStatus()
    {
        // Act
        var mutations = _service.GenerateMutations("price", "number", null, "19.99");

        // Assert
        mutations.Should().Contain(m =>
            m.MutationType == "boundary_verySmall" &&
            m.Value == "0.0000001" &&
            m.ExpectedStatusCode == 200);
        mutations.Should().Contain(m =>
            m.MutationType == "boundary_zero" &&
            m.Value == "0" &&
            m.ExpectedStatusCode == 200);
    }

    [Fact]
    public void GenerateMutations_Boolean_ShouldContainNumericBooleanMutation()
    {
        // Act
        var mutations = _service.GenerateMutations("active", "boolean", null, "true");

        // Assert
        mutations.Should().Contain(m => m.MutationType == "numericBoolean" && m.Value == "2");
    }

    [Fact]
    public void ExtractPathParameters_MoreThanMaxSupported_ShouldThrowValidationException()
    {
        // Arrange
        var placeholders = string.Join('/', Enumerable.Range(1, 11).Select(i => $"{{p{i}}}"));
        var path = $"/api/{placeholders}";

        // Act
        var act = () => _service.ExtractPathParameters(path);

        // Assert
        act.Should().Throw<ValidationException>()
            .WithMessage("*tối đa 10 path parameters*");
    }
}
