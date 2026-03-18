using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Services;
using System.Text.Json;
using HttpMethodEnum = ClassifiedAds.Modules.TestGeneration.Entities.HttpMethod;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for LlmSuggestionMaterializer.
/// Verifies materialization of LLM suggestions, persisted suggestions, and modified content
/// into TestCase domain entities, including static helper methods.
/// </summary>
public class LlmSuggestionMaterializerTests
{
    private readonly Mock<ITestCaseRequestBuilder> _requestBuilderMock;
    private readonly Mock<ITestCaseExpectationBuilder> _expectationBuilderMock;
    private readonly LlmSuggestionMaterializer _sut;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public LlmSuggestionMaterializerTests()
    {
        _requestBuilderMock = new Mock<ITestCaseRequestBuilder>();
        _expectationBuilderMock = new Mock<ITestCaseExpectationBuilder>();

        _requestBuilderMock.Setup(x => x.Build(It.IsAny<Guid>(), It.IsAny<N8nTestCaseRequest>(), It.IsAny<ApiOrderItemModel>()))
            .Returns<Guid, N8nTestCaseRequest, ApiOrderItemModel>((id, req, order) =>
                new TestCaseRequest { Id = Guid.NewGuid(), TestCaseId = id, HttpMethod = HttpMethodEnum.GET });

        _expectationBuilderMock.Setup(x => x.Build(It.IsAny<Guid>(), It.IsAny<N8nTestCaseExpectation>()))
            .Returns<Guid, N8nTestCaseExpectation>((id, exp) =>
                new TestCaseExpectation { Id = Guid.NewGuid(), TestCaseId = id });

        _sut = new LlmSuggestionMaterializer(
            _requestBuilderMock.Object,
            _expectationBuilderMock.Object);
    }

    #region MaterializeFromScenario

    [Fact]
    public void MaterializeFromScenario_Should_CreateTestCaseWithCorrectProperties()
    {
        // Arrange
        var testSuiteId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var scenario = new LlmSuggestedScenario
        {
            EndpointId = endpointId,
            ScenarioName = "Missing required field",
            Description = "Send request without required field",
            SuggestedTestType = TestType.Negative,
            ExpectedStatusCode = 400,
            ExpectedBehavior = "Should return validation error",
            Priority = "High",
            Tags = new List<string> { "validation" },
            Variables = new List<N8nTestCaseVariable>
            {
                new() { VariableName = "errorCode", ExtractFrom = "body", JsonPath = "$.code" },
            },
        };
        var orderItem = new ApiOrderItemModel { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 };

        // Act
        var result = _sut.MaterializeFromScenario(scenario, testSuiteId, orderItem, 3);

        // Assert
        result.Name.Should().Be("Missing required field");
        result.Description.Should().Be("Send request without required field");
        result.TestType.Should().Be(TestType.Negative);
        result.Priority.Should().Be(TestPriority.High);
        result.IsEnabled.Should().BeTrue();
        result.Tags.Should().NotBeNullOrWhiteSpace();
        result.Version.Should().Be(1);
        result.TestSuiteId.Should().Be(testSuiteId);
        result.EndpointId.Should().Be(endpointId);
        result.OrderIndex.Should().Be(3);
    }

    [Fact]
    public void MaterializeFromScenario_Should_CallRequestBuilder()
    {
        // Arrange
        var testSuiteId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var scenario = new LlmSuggestedScenario
        {
            EndpointId = endpointId,
            ScenarioName = "Missing required field",
            Description = "Send request without required field",
            SuggestedTestType = TestType.Negative,
            ExpectedStatusCode = 400,
            ExpectedBehavior = "Should return validation error",
            Priority = "High",
            Tags = new List<string> { "validation" },
        };
        var orderItem = new ApiOrderItemModel { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 };

        // Act
        _sut.MaterializeFromScenario(scenario, testSuiteId, orderItem, 0);

        // Assert
        _requestBuilderMock.Verify(
            x => x.Build(It.IsAny<Guid>(), It.IsAny<N8nTestCaseRequest>(), orderItem),
            Times.Once);
    }

    [Fact]
    public void MaterializeFromScenario_Should_CallExpectationBuilder()
    {
        // Arrange
        var testSuiteId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var scenario = new LlmSuggestedScenario
        {
            EndpointId = endpointId,
            ScenarioName = "Missing required field",
            Description = "Send request without required field",
            SuggestedTestType = TestType.Negative,
            ExpectedStatusCode = 400,
            ExpectedBehavior = "Should return validation error",
            Priority = "High",
            Tags = new List<string> { "validation" },
        };
        var orderItem = new ApiOrderItemModel { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 };

        // Act
        _sut.MaterializeFromScenario(scenario, testSuiteId, orderItem, 0);

        // Assert
        _expectationBuilderMock.Verify(
            x => x.Build(It.IsAny<Guid>(), It.IsAny<N8nTestCaseExpectation>()),
            Times.Once);
    }

    [Fact]
    public void MaterializeFromScenario_Should_MapVariablesCorrectly()
    {
        // Arrange
        var testSuiteId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var scenario = new LlmSuggestedScenario
        {
            EndpointId = endpointId,
            ScenarioName = "Variable mapping test",
            Description = "Test variable mapping",
            SuggestedTestType = TestType.Negative,
            ExpectedStatusCode = 400,
            Priority = "Medium",
            Tags = new List<string>(),
            Variables = new List<N8nTestCaseVariable>
            {
                new() { VariableName = "authToken", ExtractFrom = "body", JsonPath = "$.data.token", DefaultValue = "fallback" },
                new() { VariableName = "sessionId", ExtractFrom = "header", HeaderName = "X-Session-Id" },
            },
        };
        var orderItem = new ApiOrderItemModel { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 };

        // Act
        var result = _sut.MaterializeFromScenario(scenario, testSuiteId, orderItem, 0);

        // Assert
        result.Variables.Should().HaveCount(2);

        var variables = result.Variables.ToList();

        variables[0].VariableName.Should().Be("authToken");
        variables[0].ExtractFrom.Should().Be(ExtractFrom.ResponseBody);
        variables[0].JsonPath.Should().Be("$.data.token");
        variables[0].DefaultValue.Should().Be("fallback");
        variables[0].TestCaseId.Should().Be(result.Id);
        variables[0].Id.Should().NotBeEmpty();

        variables[1].VariableName.Should().Be("sessionId");
        variables[1].ExtractFrom.Should().Be(ExtractFrom.ResponseHeader);
        variables[1].HeaderName.Should().Be("X-Session-Id");
        variables[1].TestCaseId.Should().Be(result.Id);
    }

    [Fact]
    public void MaterializeFromScenario_Should_HandleNullVariables()
    {
        // Arrange
        var testSuiteId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var scenario = new LlmSuggestedScenario
        {
            EndpointId = endpointId,
            ScenarioName = "No variables scenario",
            Description = "Scenario without variables",
            SuggestedTestType = TestType.Negative,
            ExpectedStatusCode = 400,
            Priority = "Low",
            Tags = new List<string>(),
            Variables = null,
        };
        var orderItem = new ApiOrderItemModel { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 };

        // Act
        var result = _sut.MaterializeFromScenario(scenario, testSuiteId, orderItem, 0);

        // Assert
        result.Variables.Should().BeEmpty();
    }

    #endregion

    #region MaterializeFromSuggestion

    [Fact]
    public void MaterializeFromSuggestion_Should_DeserializeRequestFromJsonb()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        var suggestion = new LlmSuggestion
        {
            Id = Guid.NewGuid(),
            TestSuiteId = Guid.NewGuid(),
            EndpointId = endpointId,
            SuggestedName = "Test suggestion",
            TestType = TestType.Negative,
            Priority = TestPriority.High,
            SuggestedTags = "[\"negative\",\"auto-generated\",\"llm-suggested\"]",
            SuggestedRequest = JsonSerializer.Serialize(new N8nTestCaseRequest { HttpMethod = "POST", Url = "/api/test" }, JsonOpts),
            SuggestedExpectation = JsonSerializer.Serialize(new N8nTestCaseExpectation { ExpectedStatus = new List<int> { 400 } }, JsonOpts),
            SuggestedVariables = null,
            ReviewStatus = ReviewStatus.Pending,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
        var orderItem = new ApiOrderItemModel { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 };

        // Act
        _sut.MaterializeFromSuggestion(suggestion, orderItem, 0);

        // Assert
        _requestBuilderMock.Verify(
            x => x.Build(It.IsAny<Guid>(), It.IsAny<N8nTestCaseRequest>(), orderItem),
            Times.Once);
    }

    [Fact]
    public void MaterializeFromSuggestion_Should_HandleNullSuggestedVariables()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        var suggestion = new LlmSuggestion
        {
            Id = Guid.NewGuid(),
            TestSuiteId = Guid.NewGuid(),
            EndpointId = endpointId,
            SuggestedName = "Test suggestion",
            TestType = TestType.Negative,
            Priority = TestPriority.High,
            SuggestedTags = "[\"negative\",\"auto-generated\",\"llm-suggested\"]",
            SuggestedRequest = JsonSerializer.Serialize(new N8nTestCaseRequest { HttpMethod = "POST", Url = "/api/test" }, JsonOpts),
            SuggestedExpectation = JsonSerializer.Serialize(new N8nTestCaseExpectation { ExpectedStatus = new List<int> { 400 } }, JsonOpts),
            SuggestedVariables = null,
            ReviewStatus = ReviewStatus.Pending,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
        var orderItem = new ApiOrderItemModel { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 };

        // Act
        var result = _sut.MaterializeFromSuggestion(suggestion, orderItem, 0);

        // Assert
        result.Variables.Should().BeEmpty();
    }

    #endregion

    #region MaterializeFromModifiedContent

    [Fact]
    public void MaterializeFromModifiedContent_Should_UseModifiedName()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        var suggestion = new LlmSuggestion
        {
            Id = Guid.NewGuid(),
            TestSuiteId = Guid.NewGuid(),
            EndpointId = endpointId,
            SuggestedName = "Original name",
            TestType = TestType.Negative,
            Priority = TestPriority.High,
            SuggestedTags = "[\"negative\",\"auto-generated\"]",
            SuggestedRequest = JsonSerializer.Serialize(new N8nTestCaseRequest { HttpMethod = "POST", Url = "/api/test" }, JsonOpts),
            SuggestedExpectation = JsonSerializer.Serialize(new N8nTestCaseExpectation { ExpectedStatus = new List<int> { 400 } }, JsonOpts),
            SuggestedVariables = null,
            ReviewStatus = ReviewStatus.Pending,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
        var modified = new EditableLlmSuggestionInput
        {
            Name = "Modified name",
            Description = "Modified description",
            TestType = "Boundary",
            Priority = "Critical",
            Tags = new List<string> { "modified-tag" },
        };
        var orderItem = new ApiOrderItemModel { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 };

        // Act
        var result = _sut.MaterializeFromModifiedContent(suggestion, modified, orderItem, 0);

        // Assert
        result.Name.Should().Be("Modified name");
        result.Description.Should().Be("Modified description");
        result.TestType.Should().Be(TestType.Boundary);
        result.Priority.Should().Be(TestPriority.Critical);
    }

    [Fact]
    public void MaterializeFromModifiedContent_Should_FallbackToOriginal_WhenModifiedFieldIsNull()
    {
        // Arrange
        var endpointId = Guid.NewGuid();
        var suggestion = new LlmSuggestion
        {
            Id = Guid.NewGuid(),
            TestSuiteId = Guid.NewGuid(),
            EndpointId = endpointId,
            SuggestedName = "Original name",
            SuggestedDescription = "Original description",
            TestType = TestType.Negative,
            Priority = TestPriority.High,
            SuggestedTags = "[\"negative\",\"auto-generated\"]",
            SuggestedRequest = JsonSerializer.Serialize(new N8nTestCaseRequest { HttpMethod = "POST", Url = "/api/test" }, JsonOpts),
            SuggestedExpectation = JsonSerializer.Serialize(new N8nTestCaseExpectation { ExpectedStatus = new List<int> { 400 } }, JsonOpts),
            SuggestedVariables = null,
            ReviewStatus = ReviewStatus.Pending,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };
        var modified = new EditableLlmSuggestionInput
        {
            Name = null,
            Description = null,
            TestType = null,
            Priority = null,
            Tags = null,
        };
        var orderItem = new ApiOrderItemModel { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/test", OrderIndex = 0 };

        // Act
        var result = _sut.MaterializeFromModifiedContent(suggestion, modified, orderItem, 0);

        // Assert
        result.Name.Should().Be("Original name");
        result.Description.Should().Be("Original description");
        result.TestType.Should().Be(TestType.Negative);
        result.Priority.Should().Be(TestPriority.High);
        result.Tags.Should().Be("[\"negative\",\"auto-generated\"]");
    }

    #endregion

    #region Static helpers

    [Fact]
    public void SanitizeName_Should_TruncateTo200Chars()
    {
        // Arrange
        var longName = new string('A', 250);
        var orderItem = new ApiOrderItemModel { EndpointId = Guid.NewGuid(), HttpMethod = "GET", Path = "/api/items", OrderIndex = 0 };

        // Act
        var result = LlmSuggestionMaterializer.SanitizeName(longName, orderItem);

        // Assert
        result.Should().HaveLength(200);
        result.Should().Be(longName[..200]);
    }

    [Fact]
    public void SanitizeName_Should_ReturnFallback_WhenNameIsEmpty()
    {
        // Arrange
        var orderItem = new ApiOrderItemModel { EndpointId = Guid.NewGuid(), HttpMethod = "GET", Path = "/api/items", OrderIndex = 0 };

        // Act
        var result = LlmSuggestionMaterializer.SanitizeName(string.Empty, orderItem);

        // Assert
        result.Should().Be("GET /api/items - Boundary/Negative");
    }

    [Fact]
    public void ParsePriority_Should_ReturnMedium_WhenEmpty()
    {
        // Act
        var result = LlmSuggestionMaterializer.ParsePriority(string.Empty);

        // Assert
        result.Should().Be(TestPriority.Medium);
    }

    [Theory]
    [InlineData("critical", TestPriority.Critical)]
    [InlineData("Critical", TestPriority.Critical)]
    [InlineData("high", TestPriority.High)]
    [InlineData("High", TestPriority.High)]
    [InlineData("medium", TestPriority.Medium)]
    [InlineData("Medium", TestPriority.Medium)]
    [InlineData("low", TestPriority.Low)]
    [InlineData("Low", TestPriority.Low)]
    [InlineData("unknown", TestPriority.Medium)]
    [InlineData(null, TestPriority.Medium)]
    public void ParsePriority_Should_ParseAllValues(string input, TestPriority expected)
    {
        // Act
        var result = LlmSuggestionMaterializer.ParsePriority(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void SerializeTags_Should_IncludeSourceTag()
    {
        // Act
        var result = LlmSuggestionMaterializer.SerializeTags(TestType.Negative, "llm-suggested", "security");

        // Assert
        var tags = JsonSerializer.Deserialize<List<string>>(result, JsonOpts);
        tags.Should().NotBeNull();
        tags.Should().Contain("negative");
        tags.Should().Contain("auto-generated");
        tags.Should().Contain("llm-suggested");
        tags.Should().Contain("security");
    }

    #endregion
}
