using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for ObservationConfirmationPromptBuilder.
/// Covers: single endpoint prompts, sequence prompts, cross-endpoint context.
/// Source: COmbine/RBCTest paper (arXiv:2504.17287) Section 3 - Observation-Confirmation Prompting.
/// </summary>
public class ObservationConfirmationPromptBuilderTests
{
    private readonly ObservationConfirmationPromptBuilder _sut;

    public ObservationConfirmationPromptBuilderTests()
    {
        _sut = new ObservationConfirmationPromptBuilder();
    }

    #region BuildForEndpoint - Null/Empty Tests

    [Fact]
    public void BuildForEndpoint_Should_ReturnNull_WhenContextIsNull()
    {
        // Act
        var result = _sut.BuildForEndpoint(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void BuildForEndpoint_Should_ReturnPrompts_WhenMinimalContextProvided()
    {
        // Arrange
        var context = new EndpointPromptContext
        {
            HttpMethod = "GET",
            Path = "/api/users",
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.Should().NotBeNull();
        result.ObservationPrompt.Should().NotBeNullOrWhiteSpace();
        result.ConfirmationPromptTemplate.Should().NotBeNullOrWhiteSpace();
        result.CombinedPrompt.Should().NotBeNullOrWhiteSpace();
        result.SystemPrompt.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region BuildForEndpoint - Content Tests

    [Fact]
    public void BuildForEndpoint_Should_IncludeMethodAndPath()
    {
        // Arrange
        var context = new EndpointPromptContext
        {
            HttpMethod = "POST",
            Path = "/api/orders",
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("POST");
        result.ObservationPrompt.Should().Contain("/api/orders");
        result.CombinedPrompt.Should().Contain("POST");
        result.CombinedPrompt.Should().Contain("/api/orders");
    }

    [Fact]
    public void BuildForEndpoint_Should_IncludeOperationId_WhenProvided()
    {
        // Arrange
        var context = new EndpointPromptContext
        {
            HttpMethod = "GET",
            Path = "/api/users/{id}",
            OperationId = "getUserById",
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("getUserById");
    }

    [Fact]
    public void BuildForEndpoint_Should_IncludeSummaryAndDescription()
    {
        // Arrange
        var context = new EndpointPromptContext
        {
            HttpMethod = "POST",
            Path = "/api/users",
            Summary = "Create a new user",
            Description = "Creates a new user in the system and returns the created user.",
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("Create a new user");
        result.ObservationPrompt.Should().Contain("Creates a new user in the system");
    }

    [Fact]
    public void BuildForEndpoint_Should_IncludeParameters()
    {
        // Arrange
        var context = new EndpointPromptContext
        {
            HttpMethod = "GET",
            Path = "/api/users/{id}",
            Parameters = new List<ParameterPromptContext>
            {
                new()
                {
                    Name = "id",
                    In = "path",
                    Required = true,
                    Schema = @"{ ""type"": ""string"", ""format"": ""uuid"" }",
                    Description = "The user ID",
                },
                new()
                {
                    Name = "include",
                    In = "query",
                    Required = false,
                    Description = "Related entities to include",
                },
            },
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("id");
        result.ObservationPrompt.Should().Contain("path");
        result.ObservationPrompt.Should().Contain("required: True");
        result.ObservationPrompt.Should().Contain("The user ID");
        result.ObservationPrompt.Should().Contain("include");
        result.ObservationPrompt.Should().Contain("query");
    }

    [Fact]
    public void BuildForEndpoint_Should_IncludeRequestBodySchema()
    {
        // Arrange
        var context = new EndpointPromptContext
        {
            HttpMethod = "POST",
            Path = "/api/users",
            RequestBodySchema = @"{ ""type"": ""object"", ""properties"": { ""email"": { ""type"": ""string"" } } }",
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("Request Body Schema");
        result.ObservationPrompt.Should().Contain("email");
    }

    [Fact]
    public void BuildForEndpoint_Should_IncludeResponses()
    {
        // Arrange
        var context = new EndpointPromptContext
        {
            HttpMethod = "GET",
            Path = "/api/users",
            Responses = new List<ResponsePromptContext>
            {
                new() { StatusCode = 200, Description = "Success", Schema = @"{ ""type"": ""array"" }" },
                new() { StatusCode = 404, Description = "Not found" },
            },
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("200");
        result.ObservationPrompt.Should().Contain("Success");
        result.ObservationPrompt.Should().Contain("404");
        result.ObservationPrompt.Should().Contain("Not found");
    }

    [Fact]
    public void BuildForEndpoint_Should_IncludeExamples()
    {
        // Arrange
        var context = new EndpointPromptContext
        {
            HttpMethod = "POST",
            Path = "/api/users",
            RequestExample = @"{ ""email"": ""test@example.com"" }",
            ResponseExample = @"{ ""id"": ""123"", ""email"": ""test@example.com"" }",
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("Request Example");
        result.ObservationPrompt.Should().Contain("test@example.com");
        result.ObservationPrompt.Should().Contain("Response Example");
    }

    [Fact]
    public void BuildForEndpoint_Should_IncludeBusinessContext()
    {
        // Arrange
        var context = new EndpointPromptContext
        {
            HttpMethod = "POST",
            Path = "/api/users",
            BusinessContext = "Users must be at least 18 years old to register",
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("Business Rules");
        result.ObservationPrompt.Should().Contain("Users must be at least 18 years old");
        result.CombinedPrompt.Should().Contain("Users must be at least 18 years old");
    }

    #endregion

    #region BuildForEndpoint - Prompt Structure Tests

    [Fact]
    public void BuildForEndpoint_Should_ContainObservationPhaseInstructions()
    {
        // Arrange
        var context = CreateBasicContext();

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("Phase 1");
        result.ObservationPrompt.Should().Contain("Observation");
        result.ObservationPrompt.Should().Contain("list ALL");
    }

    [Fact]
    public void BuildForEndpoint_Should_ContainConfirmationPhaseInstructions()
    {
        // Arrange
        var context = CreateBasicContext();

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ConfirmationPromptTemplate.Should().Contain("Phase 2");
        result.ConfirmationPromptTemplate.Should().Contain("Confirmation");
        result.ConfirmationPromptTemplate.Should().Contain("CONFIRM");
    }

    [Fact]
    public void BuildForEndpoint_Should_ContainCombinedPromptSteps()
    {
        // Arrange
        var context = CreateBasicContext();

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.CombinedPrompt.Should().Contain("Step 1");
        result.CombinedPrompt.Should().Contain("Step 2");
        result.CombinedPrompt.Should().Contain("Step 3");
        result.CombinedPrompt.Should().Contain("Observe");
        result.CombinedPrompt.Should().Contain("Confirm");
        result.CombinedPrompt.Should().Contain("Output");
    }

    [Fact]
    public void BuildForEndpoint_Should_ContainConstraintCategories()
    {
        // Arrange
        var context = CreateBasicContext();

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("Type checks");
        result.ObservationPrompt.Should().Contain("Format checks");
        result.ObservationPrompt.Should().Contain("Presence checks");
        result.ObservationPrompt.Should().Contain("Value checks");
        result.ObservationPrompt.Should().Contain("Range checks");
        result.ObservationPrompt.Should().Contain("Relationship checks");
        result.ObservationPrompt.Should().Contain("Business rule checks");
    }

    [Fact]
    public void BuildForEndpoint_Should_HaveSystemPromptWithJsonFormat()
    {
        // Arrange
        var context = CreateBasicContext();

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.SystemPrompt.Should().Contain("JSON");
        result.SystemPrompt.Should().Contain("field");
        result.SystemPrompt.Should().Contain("constraint");
        result.SystemPrompt.Should().Contain("evidence");
        result.SystemPrompt.Should().Contain("assertion");
    }

    #endregion

    #region BuildForSequence Tests

    [Fact]
    public void BuildForSequence_Should_ReturnEmpty_WhenEndpointsIsNull()
    {
        // Act
        var result = _sut.BuildForSequence(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildForSequence_Should_ReturnEmpty_WhenEndpointsIsEmpty()
    {
        // Act
        var result = _sut.BuildForSequence(Array.Empty<EndpointPromptContext>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildForSequence_Should_BuildPromptForEachEndpoint()
    {
        // Arrange
        var endpoints = new List<EndpointPromptContext>
        {
            new() { HttpMethod = "POST", Path = "/api/users" },
            new() { HttpMethod = "GET", Path = "/api/users/{id}" },
            new() { HttpMethod = "PUT", Path = "/api/users/{id}" },
        };

        // Act
        var result = _sut.BuildForSequence(endpoints);

        // Assert
        result.Should().HaveCount(3);
        result.All(p => p.ObservationPrompt != null).Should().BeTrue();
        result.All(p => p.CombinedPrompt != null).Should().BeTrue();
    }

    [Fact]
    public void BuildForSequence_Should_AddCrossEndpointContext_ForRelatedEndpoints()
    {
        // Arrange: POST and GET share "users" path segment
        var endpoints = new List<EndpointPromptContext>
        {
            new() { HttpMethod = "POST", Path = "/api/users", Summary = "Create user" },
            new() { HttpMethod = "GET", Path = "/api/users/{id}", Summary = "Get user by ID" },
        };

        // Act
        var result = _sut.BuildForSequence(endpoints);

        // Assert: Second prompt should have cross-endpoint context
        result.Should().HaveCount(2);
        result[1].ObservationPrompt.Should().Contain("Cross-Endpoint Context");
        result[1].ObservationPrompt.Should().Contain("POST");
        result[1].ObservationPrompt.Should().Contain("/api/users");
    }

    [Fact]
    public void BuildForSequence_Should_NotAddCrossEndpointContext_ForUnrelatedEndpoints()
    {
        // Arrange: Endpoints don't share path segments (no common segment)
        // Note: /api/users and /api/orders share "api", so use completely different paths
        var endpoints = new List<EndpointPromptContext>
        {
            new() { HttpMethod = "POST", Path = "/users" },
            new() { HttpMethod = "GET", Path = "/orders" }, // No shared segments
        };

        // Act
        var result = _sut.BuildForSequence(endpoints);

        // Assert: Second prompt should NOT have cross-endpoint context
        result.Should().HaveCount(2);
        result[1].ObservationPrompt.Should().NotContain("Cross-Endpoint Context");
    }

    [Fact]
    public void BuildForSequence_Should_IncludeAllPreviousRelatedEndpoints()
    {
        // Arrange
        var endpoints = new List<EndpointPromptContext>
        {
            new() { HttpMethod = "POST", Path = "/api/users", Summary = "Create user" },
            new() { HttpMethod = "PUT", Path = "/api/users/{id}", Summary = "Update user" },
            new() { HttpMethod = "DELETE", Path = "/api/users/{id}", Summary = "Delete user" },
        };

        // Act
        var result = _sut.BuildForSequence(endpoints);

        // Assert: Third prompt should reference both previous endpoints
        result[2].ObservationPrompt.Should().Contain("POST /api/users");
        result[2].ObservationPrompt.Should().Contain("PUT /api/users/{id}");
    }

    #endregion

    #region Schema Truncation Tests

    [Fact]
    public void BuildForEndpoint_Should_TruncateLongSchemas()
    {
        // Arrange
        var longSchema = new string('x', 2000);
        var context = new EndpointPromptContext
        {
            HttpMethod = "POST",
            Path = "/api/users",
            RequestBodySchema = longSchema,
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ObservationPrompt.Should().Contain("truncated");
        result.ObservationPrompt.Length.Should().BeLessThan(longSchema.Length + 1000);
    }

    #endregion

    #region Confirmation Template Tests

    [Fact]
    public void BuildForEndpoint_Should_IncludeMethodAndPathInConfirmationTemplate()
    {
        // Arrange
        var context = new EndpointPromptContext
        {
            HttpMethod = "DELETE",
            Path = "/api/users/{id}",
        };

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ConfirmationPromptTemplate.Should().Contain("DELETE");
        result.ConfirmationPromptTemplate.Should().Contain("/api/users/{id}");
    }

    [Fact]
    public void BuildForEndpoint_Should_HavePlaceholder_ForObservationResults()
    {
        // Arrange
        var context = CreateBasicContext();

        // Act
        var result = _sut.BuildForEndpoint(context);

        // Assert
        result.ConfirmationPromptTemplate.Should().Contain("{OBSERVATION_RESULTS}");
    }

    #endregion

    #region Helpers

    private static EndpointPromptContext CreateBasicContext()
    {
        return new EndpointPromptContext
        {
            HttpMethod = "GET",
            Path = "/api/users",
            OperationId = "getUsers",
            Summary = "Get all users",
        };
    }

    #endregion
}
