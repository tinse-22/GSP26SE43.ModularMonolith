using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// FE-06: BodyMutationEngine unit tests.
/// Verifies rule-based body mutation generation: empty body, malformed JSON,
/// missing required fields, type mismatches, overflow, and invalid enum mutations.
/// </summary>
public class BodyMutationEngineTests
{
    private readonly BodyMutationEngine _engine = new();

    [Fact]
    public void GenerateMutations_Should_ReturnEmptyBodyMutations_ForPostMethod()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/items",
            BodyParameters = Array.Empty<ParameterDetailDto>(),
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert
        var emptyBodyMutations = mutations.Where(m => m.MutationType == "emptyBody").ToList();
        var malformedJsonMutations = mutations.Where(m => m.MutationType == "malformedJson").ToList();

        emptyBodyMutations.Should().HaveCount(3);
        malformedJsonMutations.Should().HaveCount(3);
        mutations.Should().HaveCountGreaterThanOrEqualTo(6);
    }

    [Fact]
    public void GenerateMutations_Should_ReturnMalformedJsonMutations_ForPostMethod()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/orders",
            BodyParameters = Array.Empty<ParameterDetailDto>(),
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert
        var malformedMutations = mutations.Where(m => m.MutationType == "malformedJson").ToList();

        malformedMutations.Should().HaveCount(3);
        malformedMutations.Should().Contain(m => m.Label.Contains("missing closing brace"));
        malformedMutations.Should().Contain(m => m.Label.Contains("truncated value"));
        malformedMutations.Should().Contain(m => m.Label.Contains("plain text"));
        malformedMutations.Should().OnlyContain(m => m.SuggestedTestType == TestType.Negative);
    }

    [Fact]
    public void GenerateMutations_Should_GenerateMissingRequiredField_ForEachRequiredParam()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/users",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "email",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = true,
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "password",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = true,
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "nickname",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = false,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert
        var missingRequired = mutations.Where(m => m.MutationType == "missingRequired").ToList();

        missingRequired.Should().HaveCount(2);
        missingRequired.Should().Contain(m => m.TargetFieldName == "email");
        missingRequired.Should().Contain(m => m.TargetFieldName == "password");
        missingRequired.Should().NotContain(m => m.TargetFieldName == "nickname");
    }

    [Fact]
    public void GenerateMutations_Should_GenerateTypeMismatch_ForIntegerField()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "PUT",
            Path = "/api/products/{id}",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "quantity",
                    Location = "Body",
                    DataType = "integer",
                    Format = "int32",
                    IsRequired = true,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert
        var typeMismatch = mutations
            .Where(m => m.MutationType == "typeMismatch" && m.TargetFieldName == "quantity")
            .ToList();

        typeMismatch.Should().HaveCount(1);
        typeMismatch[0].MutatedBody.Should().Contain("not_a_number");
        typeMismatch[0].SuggestedTestType.Should().Be(TestType.Negative);
        typeMismatch[0].ExpectedStatusCode.Should().Be(400);
    }

    [Fact]
    public void GenerateMutations_Should_GenerateTypeMismatch_ForStringField()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/contacts",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "fullName",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = true,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert
        var typeMismatch = mutations
            .Where(m => m.MutationType == "typeMismatch" && m.TargetFieldName == "fullName")
            .ToList();

        typeMismatch.Should().HaveCount(1);
        typeMismatch[0].MutatedBody.Should().Contain("12345");
        typeMismatch[0].SuggestedTestType.Should().Be(TestType.Negative);
    }

    [Fact]
    public void GenerateMutations_Should_GenerateOverflow_ForInt32Field()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "PATCH",
            Path = "/api/inventory/{id}",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "stock",
                    Location = "Body",
                    DataType = "integer",
                    Format = "int32",
                    IsRequired = true,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert
        var overflow = mutations
            .Where(m => m.MutationType == "overflow" && m.TargetFieldName == "stock")
            .ToList();

        overflow.Should().HaveCount(1);
        overflow[0].MutatedBody.Should().Contain("2147483648");
        overflow[0].Label.Should().Contain("Int32.MaxValue + 1");
    }

    [Fact]
    public void GenerateMutations_Should_GenerateInvalidEnum_WhenSchemaHasEnum()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/tickets",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "priority",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = true,
                    Schema = "{\"enum\":[\"Low\",\"Medium\",\"High\"]}",
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert
        var invalidEnum = mutations
            .Where(m => m.MutationType == "invalidEnum" && m.TargetFieldName == "priority")
            .ToList();

        invalidEnum.Should().HaveCount(1);
        invalidEnum[0].MutatedBody.Should().Contain("INVALID_ENUM_VALUE_");
        invalidEnum[0].SuggestedTestType.Should().Be(TestType.Negative);
        invalidEnum[0].ExpectedStatusCode.Should().Be(400);
        invalidEnum[0].Description.Should().Contain("Low");
        invalidEnum[0].Description.Should().Contain("Medium");
        invalidEnum[0].Description.Should().Contain("High");
    }

    [Fact]
    public void GenerateMutations_Should_HandleEmptyParameterList_ReturningOnlyWholeBodyMutations()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/actions",
            BodyParameters = new List<ParameterDetailDto>(),
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert - with empty params and no schema, only whole-body mutations are generated
        mutations.Should().HaveCount(6);

        var mutationTypes = mutations.Select(m => m.MutationType).Distinct().ToList();
        mutationTypes.Should().HaveCount(2);
        mutationTypes.Should().Contain("emptyBody");
        mutationTypes.Should().Contain("malformedJson");

        // No per-field mutations should be present
        mutations.Should().NotContain(m => m.MutationType == "missingRequired");
        mutations.Should().NotContain(m => m.MutationType == "typeMismatch");
        mutations.Should().NotContain(m => m.MutationType == "overflow");
        mutations.Should().NotContain(m => m.MutationType == "invalidEnum");
    }

    [Fact]
    public void GenerateMutations_Should_SetCorrectTestType_BoundaryForOverflow()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "PUT",
            Path = "/api/settings/{id}",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "maxRetries",
                    Location = "Body",
                    DataType = "integer",
                    Format = "int32",
                    IsRequired = false,
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "description",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = false,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert
        var overflowMutations = mutations.Where(m => m.MutationType == "overflow").ToList();

        overflowMutations.Should().HaveCountGreaterThanOrEqualTo(2);
        overflowMutations.Should().OnlyContain(m => m.SuggestedTestType == TestType.Boundary);

        // Verify integer overflow uses Int32.MaxValue + 1
        var intOverflow = overflowMutations.First(m => m.TargetFieldName == "maxRetries");
        intOverflow.SuggestedTestType.Should().Be(TestType.Boundary);

        // Verify string overflow uses 10000-char string
        var strOverflow = overflowMutations.First(m => m.TargetFieldName == "description");
        strOverflow.SuggestedTestType.Should().Be(TestType.Boundary);
        strOverflow.Label.Should().Contain("10000-char string");
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("DELETE")]
    [InlineData("HEAD")]
    [InlineData("OPTIONS")]
    public void GenerateMutations_Should_ReturnEmptyList_ForNonBodyMethods(string httpMethod)
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = httpMethod,
            Path = "/api/items/{id}",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "name",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = true,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert
        mutations.Should().BeEmpty();
    }

    [Fact]
    public void GenerateMutations_Should_SkipOverflow_ForBooleanArrayObjectFields()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/data",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "isActive",
                    Location = "Body",
                    DataType = "boolean",
                    IsRequired = false,
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "tags",
                    Location = "Body",
                    DataType = "array",
                    IsRequired = false,
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "metadata",
                    Location = "Body",
                    DataType = "object",
                    IsRequired = false,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert — overflow mutations should NOT include boolean, array, or object fields
        var overflowMutations = mutations.Where(m => m.MutationType == "overflow").ToList();
        overflowMutations.Should().BeEmpty();
    }

    [Fact]
    public void GenerateMutations_Should_UseDefaultValueFromParameter_InBaseBody()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/items",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "quantity",
                    Location = "Body",
                    DataType = "integer",
                    IsRequired = true,
                    DefaultValue = "42",
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "label",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = true,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert — missingRequired for "quantity" should produce body that includes "label" with default "sample_value"
        var missingQuantity = mutations
            .First(m => m.MutationType == "missingRequired" && m.TargetFieldName == "quantity");
        missingQuantity.MutatedBody.Should().Contain("sample_value");
        missingQuantity.MutatedBody.Should().NotContain("quantity");

        // missingRequired for "label" should produce body that includes "quantity" with its default "42"
        var missingLabel = mutations
            .First(m => m.MutationType == "missingRequired" && m.TargetFieldName == "label");
        missingLabel.MutatedBody.Should().Contain("42");
        missingLabel.MutatedBody.Should().NotContain("label");
    }

    [Fact]
    public void GenerateMutations_Should_AssignExpectedStatuses_ByMutationPolicy()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/complex",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "name",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = true,
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "age",
                    Location = "Body",
                    DataType = "integer",
                    Format = "int32",
                    IsRequired = true,
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "status",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = false,
                    Schema = "{\"enum\":[\"Active\",\"Inactive\"]}",
                },
            },
            RequestBodySchema = "{\"properties\":{\"extraField\":{\"type\":\"string\"}},\"required\":[\"extraField\"]}",
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert - expected statuses should follow mutation-type policy
        mutations.Should().NotBeEmpty();

        var emptyBodyMutations = mutations.Where(m => m.MutationType == "emptyBody").ToList();
        emptyBodyMutations.Should().NotBeEmpty();
        emptyBodyMutations.Should().OnlyContain(m =>
            m.GetEffectiveExpectedStatusCodes().SequenceEqual(new[] { 400, 415, 422 }));

        var malformedJsonMutations = mutations.Where(m => m.MutationType == "malformedJson").ToList();
        malformedJsonMutations.Should().NotBeEmpty();
        malformedJsonMutations.Should().OnlyContain(m =>
            m.GetEffectiveExpectedStatusCodes().SequenceEqual(new[] { 400 }));

        var validationMutations = mutations.Where(m =>
            m.MutationType is "missingRequired" or "typeMismatch" or "overflow" or "invalidEnum").ToList();
        validationMutations.Should().NotBeEmpty();
        validationMutations.Should().OnlyContain(m =>
            m.GetEffectiveExpectedStatusCodes().SequenceEqual(new[] { 400, 422 }));

        // Backward-compatibility: primary status remains the first allowed status.
        mutations.Should().OnlyContain(m =>
            m.ExpectedStatusCode == m.GetEffectiveExpectedStatusCodes().First());

        // Verify we have a variety of mutation types
        var types = mutations.Select(m => m.MutationType).Distinct().ToList();
        types.Should().Contain("emptyBody");
        types.Should().Contain("malformedJson");
        types.Should().Contain("missingRequired");
        types.Should().Contain("typeMismatch");
        types.Should().Contain("overflow");
        types.Should().Contain("invalidEnum");
    }

    [Fact]
    public void GenerateMutations_Should_UseJsonObjectDefault_InBaseBody()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/configs",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "settings",
                    Location = "Body",
                    DataType = "object",
                    IsRequired = true,
                    DefaultValue = "{\"mode\":\"strict\",\"retries\":3}",
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "name",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = true,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert — missingRequired for "name" should produce body containing settings as a real JSON object
        var missingName = mutations
            .First(m => m.MutationType == "missingRequired" && m.TargetFieldName == "name");
        missingName.MutatedBody.Should().Contain("\"mode\"");
        missingName.MutatedBody.Should().Contain("\"strict\"");
        missingName.MutatedBody.Should().Contain("\"retries\"");
        missingName.MutatedBody.Should().Contain("3");
        // Should NOT be a raw string like "\"settings\":\"{\\\"mode\\\":\\\"strict\\\"\""
        missingName.MutatedBody.Should().NotContain("\\\"");
    }

    [Fact]
    public void GenerateMutations_Should_UseJsonArrayDefault_InBaseBody()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/bulk",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "ids",
                    Location = "Body",
                    DataType = "array",
                    IsRequired = true,
                    DefaultValue = "[1,2,3]",
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "label",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = true,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert — missingRequired for "label" should produce body containing ids as a real JSON array
        var missingLabel = mutations
            .First(m => m.MutationType == "missingRequired" && m.TargetFieldName == "label");
        missingLabel.MutatedBody.Should().Contain("[1,2,3]");
        // Should NOT be a raw string like "\"ids\":\"[1,2,3]\""
        missingLabel.MutatedBody.Should().NotContain("\"[1,2,3]\"");
    }

    [Fact]
    public void GenerateMutations_Should_UseJsonArrayExample_InBaseBody()
    {
        // Arrange
        var context = new BodyMutationContext
        {
            EndpointId = Guid.NewGuid(),
            HttpMethod = "POST",
            Path = "/api/tags",
            BodyParameters = new List<ParameterDetailDto>
            {
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "tags",
                    Location = "Body",
                    DataType = "array",
                    IsRequired = true,
                    Examples = "[\"alpha\",\"beta\"]",
                },
                new()
                {
                    ParameterId = Guid.NewGuid(),
                    Name = "title",
                    Location = "Body",
                    DataType = "string",
                    IsRequired = true,
                },
            },
        };

        // Act
        var mutations = _engine.GenerateMutations(context);

        // Assert — missingRequired for "title" should produce body containing tags as a real JSON array
        var missingTitle = mutations
            .First(m => m.MutationType == "missingRequired" && m.TargetFieldName == "title");
        missingTitle.MutatedBody.Should().Contain("\"alpha\"");
        missingTitle.MutatedBody.Should().Contain("\"beta\"");
    }
}
