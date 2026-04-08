using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using HttpMethodEnum = ClassifiedAds.Modules.TestGeneration.Entities.HttpMethod;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// FE-06: BoundaryNegativeTestCaseGenerator unit tests.
/// Verifies orchestration of path mutations, body mutations, and LLM scenario suggestions,
/// including flag-based source filtering, sequential OrderIndex assignment, test type classification,
/// tag assignment, and handling of empty metadata.
/// </summary>
public class BoundaryNegativeTestCaseGeneratorTests
{
    private readonly Mock<IApiEndpointMetadataService> _endpointMetadataServiceMock;
    private readonly Mock<IApiEndpointParameterDetailService> _parameterDetailServiceMock;
    private readonly Mock<IPathParameterMutationGatewayService> _pathMutationServiceMock;
    private readonly Mock<IBodyMutationEngine> _bodyMutationEngineMock;
    private readonly Mock<ILlmScenarioSuggester> _llmSuggesterMock;
    private readonly Mock<ITestCaseRequestBuilder> _requestBuilderMock;
    private readonly Mock<ITestCaseExpectationBuilder> _expectationBuilderMock;
    private readonly BoundaryNegativeTestCaseGenerator _generator;

    private static readonly Guid DefaultSuiteId = Guid.NewGuid();
    private static readonly Guid DefaultSpecId = Guid.NewGuid();
    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid Endpoint1Id = Guid.NewGuid();
    private static readonly Guid Endpoint2Id = Guid.NewGuid();

    public BoundaryNegativeTestCaseGeneratorTests()
    {
        _endpointMetadataServiceMock = new Mock<IApiEndpointMetadataService>();
        _parameterDetailServiceMock = new Mock<IApiEndpointParameterDetailService>();
        _pathMutationServiceMock = new Mock<IPathParameterMutationGatewayService>();
        _bodyMutationEngineMock = new Mock<IBodyMutationEngine>();
        _llmSuggesterMock = new Mock<ILlmScenarioSuggester>();
        _requestBuilderMock = new Mock<ITestCaseRequestBuilder>();
        _expectationBuilderMock = new Mock<ITestCaseExpectationBuilder>();

        _requestBuilderMock.Setup(x => x.Build(It.IsAny<Guid>(), It.IsAny<N8nTestCaseRequest>(), It.IsAny<ApiOrderItemModel>()))
            .Returns<Guid, N8nTestCaseRequest, ApiOrderItemModel>((id, req, order) => new TestCaseRequest { Id = Guid.NewGuid(), TestCaseId = id, HttpMethod = HttpMethodEnum.GET });
        _expectationBuilderMock.Setup(x => x.Build(It.IsAny<Guid>(), It.IsAny<N8nTestCaseExpectation>()))
            .Returns<Guid, N8nTestCaseExpectation>((id, exp) => new TestCaseExpectation { Id = Guid.NewGuid(), TestCaseId = id });

        var materializer = new LlmSuggestionMaterializer(
            _requestBuilderMock.Object,
            _expectationBuilderMock.Object);

        _generator = new BoundaryNegativeTestCaseGenerator(
            _endpointMetadataServiceMock.Object,
            _parameterDetailServiceMock.Object,
            _pathMutationServiceMock.Object,
            _bodyMutationEngineMock.Object,
            _llmSuggesterMock.Object,
            _requestBuilderMock.Object,
            _expectationBuilderMock.Object,
            materializer,
            new Mock<ILogger<BoundaryNegativeTestCaseGenerator>>().Object);
    }

    [Fact]
    public async Task GenerateAsync_Should_OnlyIncludePathMutations_WhenOnlyPathFlagTrue()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);
        SetupParameterDetails(Endpoint1Id, CreatePathParameter("id", "integer", "int64"));
        SetupPathMutations("id", new PathParameterMutationDto
        {
            MutationType = "boundary_zero",
            Label = "id - Zero value",
            Value = "0",
            ExpectedStatusCode = 400,
            Description = "Test zero value for id",
        });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = true,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = false,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(1);
        result.PathMutationCount.Should().Be(1);
        result.BodyMutationCount.Should().Be(0);
        result.LlmSuggestionCount.Should().Be(0);

        _bodyMutationEngineMock.Verify(
            x => x.GenerateMutations(It.IsAny<BodyMutationContext>()), Times.Never);
        _llmSuggesterMock.Verify(
            x => x.SuggestScenariosAsync(It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_Should_OnlyIncludeBodyMutations_WhenOnlyBodyFlagTrue()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);
        SetupParameterDetails(Endpoint1Id, CreateBodyParameter("email", "string", "email"));
        SetupBodyMutations(new BodyMutation
        {
            MutationType = "typeMismatch",
            Label = "email - Type mismatch",
            MutatedBody = "{\"email\": 12345}",
            TargetFieldName = "email",
            ExpectedStatusCode = 422,
            Description = "Send numeric value for email field",
            SuggestedTestType = TestType.Negative,
        });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = false,
            IncludeBodyMutations = true,
            IncludeLlmSuggestions = false,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(1);
        result.BodyMutationCount.Should().Be(1);
        result.PathMutationCount.Should().Be(0);
        result.LlmSuggestionCount.Should().Be(0);

        _pathMutationServiceMock.Verify(
            x => x.GenerateMutations(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _llmSuggesterMock.Verify(
            x => x.SuggestScenariosAsync(It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_Should_PreserveBodyMutationExpectedStatuses_WhenBuildingExpectation()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);
        SetupParameterDetails(Endpoint1Id, CreateBodyParameter("email", "string", "email"));
        SetupBodyMutations(new BodyMutation
        {
            MutationType = "typeMismatch",
            Label = "email - Type mismatch",
            MutatedBody = "{\"email\": 12345}",
            TargetFieldName = "email",
            ExpectedStatusCode = 400,
            ExpectedStatusCodes = new List<int> { 400, 422 },
            Description = "Send numeric value for email field",
            SuggestedTestType = TestType.Negative,
        });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = false,
            IncludeBodyMutations = true,
            IncludeLlmSuggestions = false,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(1);

        var expectedStatuses = JsonSerializer.Deserialize<List<int>>(
            result.TestCases[0].Expectation.ExpectedStatus);
        expectedStatuses.Should().Equal(400, 422);
    }

    [Fact]
    public async Task GenerateAsync_Should_OnlyIncludeLlmSuggestions_WhenOnlyLlmFlagTrue()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);
        SetupLlmSuggestions(new LlmSuggestedScenario
        {
            EndpointId = Endpoint1Id,
            ScenarioName = "SQL injection in login",
            Description = "Attempt SQL injection via username field",
            SuggestedTestType = TestType.Negative,
            SuggestedBody = "{\"username\": \"' OR 1=1 --\"}",
            ExpectedStatusCode = 400,
            Priority = "High",
            Tags = new List<string> { "security", "injection" },
        });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = false,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = true,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(1);
        result.LlmSuggestionCount.Should().Be(1);
        result.PathMutationCount.Should().Be(0);
        result.BodyMutationCount.Should().Be(0);
        result.LlmModel.Should().Be("gpt-4o");
        result.LlmTokensUsed.Should().Be(500);

        _pathMutationServiceMock.Verify(
            x => x.GenerateMutations(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _bodyMutationEngineMock.Verify(
            x => x.GenerateMutations(It.IsAny<BodyMutationContext>()), Times.Never);
        // Parameter details should NOT be fetched when both path and body flags are false
        _parameterDetailServiceMock.Verify(
            x => x.GetParameterDetailsAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GenerateAsync_Should_CombineAllSources_WhenAllFlagsTrue()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);
        SetupParameterDetails(Endpoint1Id,
            CreatePathParameter("id", "integer", "int64"),
            CreateBodyParameter("name", "string", null));
        SetupPathMutations("id",
            new PathParameterMutationDto { MutationType = "boundary_zero", Label = "id - Zero", Value = "0", ExpectedStatusCode = 400, Description = "Zero id" },
            new PathParameterMutationDto { MutationType = "wrongType", Label = "id - String", Value = "abc", ExpectedStatusCode = 400, Description = "Wrong type" });
        SetupBodyMutations(
            new BodyMutation { MutationType = "missingRequired", Label = "name - Missing", MutatedBody = "{}", TargetFieldName = "name", ExpectedStatusCode = 400, Description = "Missing name", SuggestedTestType = TestType.Negative });
        SetupLlmSuggestions(
            new LlmSuggestedScenario { EndpointId = Endpoint1Id, ScenarioName = "Overflow name", Description = "Name too long", SuggestedTestType = TestType.Boundary, ExpectedStatusCode = 400, Priority = "Medium", Tags = new List<string>() },
            new LlmSuggestedScenario { EndpointId = Endpoint1Id, ScenarioName = "Empty body", Description = "No body", SuggestedTestType = TestType.Negative, ExpectedStatusCode = 400, Priority = "Low", Tags = new List<string>() });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = true,
            IncludeBodyMutations = true,
            IncludeLlmSuggestions = true,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(5); // 2 path + 1 body + 2 LLM
        result.PathMutationCount.Should().Be(2);
        result.BodyMutationCount.Should().Be(1);
        result.LlmSuggestionCount.Should().Be(2);
        result.EndpointsCovered.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateAsync_Should_AssignSequentialOrderIndex()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);
        SetupParameterDetails(Endpoint1Id, CreatePathParameter("id", "integer", "int64"));
        SetupPathMutations("id",
            new PathParameterMutationDto { MutationType = "boundary_zero", Label = "Zero", Value = "0", ExpectedStatusCode = 400, Description = "Zero" },
            new PathParameterMutationDto { MutationType = "empty", Label = "Empty", Value = "", ExpectedStatusCode = 400, Description = "Empty" },
            new PathParameterMutationDto { MutationType = "wrongType", Label = "Wrong", Value = "abc", ExpectedStatusCode = 400, Description = "Wrong" });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = true,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = false,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(3);
        for (int i = 0; i < result.TestCases.Count; i++)
        {
            result.TestCases[i].OrderIndex.Should().Be(i, $"TestCase at index {i} should have OrderIndex = {i}");
        }
    }

    [Fact]
    public async Task GenerateAsync_Should_SetCorrectTestType_PerSource()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);
        SetupParameterDetails(Endpoint1Id,
            CreatePathParameter("id", "integer", "int64"),
            CreateBodyParameter("email", "string", "email"));

        // Path mutation with "boundary" type -> TestType.Boundary
        SetupPathMutations("id",
            new PathParameterMutationDto { MutationType = "boundary_zero", Label = "Zero", Value = "0", ExpectedStatusCode = 400, Description = "Zero" });

        // Body mutation with SuggestedTestType = Negative
        SetupBodyMutations(
            new BodyMutation { MutationType = "missingRequired", Label = "Missing email", MutatedBody = "{}", TargetFieldName = "email", ExpectedStatusCode = 400, Description = "Missing email", SuggestedTestType = TestType.Negative });

        // LLM suggestion with SuggestedTestType = Boundary
        SetupLlmSuggestions(
            new LlmSuggestedScenario { EndpointId = Endpoint1Id, ScenarioName = "Max length test", Description = "Exceeds max length", SuggestedTestType = TestType.Boundary, ExpectedStatusCode = 400, Priority = "High", Tags = new List<string>() });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = true,
            IncludeBodyMutations = true,
            IncludeLlmSuggestions = true,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(3);

        // Path mutation with "boundary_zero" -> classified as Boundary
        result.TestCases[0].TestType.Should().Be(TestType.Boundary);

        // Body mutation -> uses SuggestedTestType from BodyMutation (Negative)
        result.TestCases[1].TestType.Should().Be(TestType.Negative);

        // LLM suggestion -> uses SuggestedTestType from LlmSuggestedScenario (Boundary)
        result.TestCases[2].TestType.Should().Be(TestType.Boundary);
    }

    [Fact]
    public async Task GenerateAsync_Should_SetCorrectTags_PerSource()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);
        SetupParameterDetails(Endpoint1Id,
            CreatePathParameter("id", "integer", "int64"),
            CreateBodyParameter("email", "string", "email"));

        SetupPathMutations("id",
            new PathParameterMutationDto { MutationType = "wrongType", Label = "id - Wrong type", Value = "abc", ExpectedStatusCode = 400, Description = "Wrong type" });
        SetupBodyMutations(
            new BodyMutation { MutationType = "typeMismatch", Label = "email - Type mismatch", MutatedBody = "{\"email\": 123}", TargetFieldName = "email", ExpectedStatusCode = 422, Description = "Type mismatch", SuggestedTestType = TestType.Negative });
        SetupLlmSuggestions(
            new LlmSuggestedScenario { EndpointId = Endpoint1Id, ScenarioName = "Auth bypass", Description = "Bypass auth", SuggestedTestType = TestType.Negative, ExpectedStatusCode = 401, Priority = "Critical", Tags = new List<string> { "security" } });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = true,
            IncludeBodyMutations = true,
            IncludeLlmSuggestions = true,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(3);

        // Path mutation tags should include "path-mutation"
        result.TestCases[0].Tags.Should().Contain("path-mutation");
        result.TestCases[0].Tags.Should().Contain("rule-based");
        result.TestCases[0].Tags.Should().Contain("auto-generated");

        // Body mutation tags should include "body-mutation"
        result.TestCases[1].Tags.Should().Contain("body-mutation");
        result.TestCases[1].Tags.Should().Contain("rule-based");
        result.TestCases[1].Tags.Should().Contain("auto-generated");

        // LLM suggestion tags should include "llm-suggested"
        result.TestCases[2].Tags.Should().Contain("llm-suggested");
        result.TestCases[2].Tags.Should().Contain("auto-generated");
    }

    [Fact]
    public async Task GenerateAsync_Should_CountDistinctEndpointsCovered()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = new List<ApiOrderItemModel>
        {
            new() { EndpointId = Endpoint1Id, HttpMethod = "POST", Path = "/api/users", OrderIndex = 0 },
            new() { EndpointId = Endpoint2Id, HttpMethod = "GET", Path = "/api/users/{id}", OrderIndex = 1 },
        };

        var metadata = endpoints.Select(e => new ApiEndpointMetadataDto
        {
            EndpointId = e.EndpointId,
            HttpMethod = e.HttpMethod,
            Path = e.Path,
            OperationId = $"op_{e.HttpMethod}",
            ParameterSchemaPayloads = new List<string>(),
            ResponseSchemaPayloads = new List<string>(),
        }).ToList();

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);

        SetupLlmSuggestions(
            new LlmSuggestedScenario { EndpointId = Endpoint1Id, ScenarioName = "S1", Description = "D1", SuggestedTestType = TestType.Negative, ExpectedStatusCode = 400, Tags = new List<string>() },
            new LlmSuggestedScenario { EndpointId = Endpoint2Id, ScenarioName = "S2", Description = "D2", SuggestedTestType = TestType.Negative, ExpectedStatusCode = 400, Tags = new List<string>() },
            new LlmSuggestedScenario { EndpointId = Endpoint1Id, ScenarioName = "S3", Description = "D3", SuggestedTestType = TestType.Boundary, ExpectedStatusCode = 400, Tags = new List<string>() });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = false,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = true,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert — 3 test cases but only 2 distinct EndpointIds
        result.TestCases.Should().HaveCount(3);
        result.EndpointsCovered.Should().Be(2);
    }

    [Theory]
    [InlineData("boundary_zero", TestType.Boundary)]
    [InlineData("boundary_max", TestType.Boundary)]
    [InlineData("overflow_int64", TestType.Boundary)]
    [InlineData("zero_value", TestType.Boundary)]
    [InlineData("max_int32", TestType.Boundary)]
    [InlineData("wrongType", TestType.Negative)]
    [InlineData("empty", TestType.Negative)]
    [InlineData("injection", TestType.Negative)]
    [InlineData("specialChars", TestType.Negative)]
    [InlineData("", TestType.Negative)]
    [InlineData(null, TestType.Negative)]
    public async Task GenerateAsync_Should_ClassifyPathMutationType_Correctly(string mutationType, TestType expectedTestType)
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);
        SetupParameterDetails(Endpoint1Id, CreatePathParameter("id", "integer", "int64"));
        SetupPathMutations("id", new PathParameterMutationDto
        {
            MutationType = mutationType,
            Label = "test mutation",
            Value = "0",
            ExpectedStatusCode = 400,
            Description = "Test classification",
        });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = true,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = false,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(1);
        result.TestCases[0].TestType.Should().Be(expectedTestType);
    }

    [Fact]
    public async Task GenerateAsync_Should_TruncateName_WhenScenarioNameExceeds200Chars()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);

        var longName = new string('X', 250);
        SetupLlmSuggestions(new LlmSuggestedScenario
        {
            EndpointId = Endpoint1Id,
            ScenarioName = longName,
            Description = "Very long scenario name",
            SuggestedTestType = TestType.Negative,
            ExpectedStatusCode = 400,
            Tags = new List<string>(),
        });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = false,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = true,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(1);
        result.TestCases[0].Name.Length.Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public async Task GenerateAsync_Should_UseFallbackName_WhenScenarioNameIsNull()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);

        SetupLlmSuggestions(new LlmSuggestedScenario
        {
            EndpointId = Endpoint1Id,
            ScenarioName = null,
            Description = "Scenario without name",
            SuggestedTestType = TestType.Negative,
            ExpectedStatusCode = 400,
            Tags = new List<string>(),
        });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = false,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = true,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(1);
        result.TestCases[0].Name.Should().Contain("POST");
        result.TestCases[0].Name.Should().Contain("/api/users");
        result.TestCases[0].Name.Should().Contain("Boundary/Negative");
    }

    [Fact]
    public async Task GenerateAsync_Should_HandleNoEndpointMetadata()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();

        // Return empty metadata
        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ApiEndpointMetadataDto>());
        _parameterDetailServiceMock.Setup(x => x.GetParameterDetailsAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<EndpointParameterDetailDto>());
        _bodyMutationEngineMock.Setup(x => x.GenerateMutations(It.IsAny<BodyMutationContext>()))
            .Returns(Array.Empty<BodyMutation>());
        _llmSuggesterMock.Setup(x => x.SuggestScenariosAsync(It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmScenarioSuggestionResult
            {
                Scenarios = Array.Empty<LlmSuggestedScenario>(),
                LlmModel = "gpt-4o",
                TokensUsed = 0,
            });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = true,
            IncludeBodyMutations = true,
            IncludeLlmSuggestions = true,
            UserId = DefaultUserId,
        };

        // Act
        var act = () => _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        var result = await act.Should().NotThrowAsync();
        result.Subject.TestCases.Should().BeEmpty();
        result.Subject.PathMutationCount.Should().Be(0);
        result.Subject.BodyMutationCount.Should().Be(0);
        result.Subject.LlmSuggestionCount.Should().Be(0);
        result.Subject.EndpointsCovered.Should().Be(0);
    }

    [Fact]
    public async Task GenerateAsync_Should_ConvertLlmVariables_IntoTestCaseVariables()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);

        var scenarioWithVariables = new LlmSuggestedScenario
        {
            EndpointId = Endpoint1Id,
            ScenarioName = "Login with token extraction",
            Description = "Login and extract auth token for chaining",
            SuggestedTestType = TestType.Negative,
            SuggestedBody = "{\"email\": \"test@test.com\"}",
            ExpectedStatusCode = 400,
            Priority = "High",
            Tags = new List<string> { "auth" },
            Variables = new List<N8nTestCaseVariable>
            {
                new()
                {
                    VariableName = "authToken",
                    ExtractFrom = "ResponseBody",
                    JsonPath = "$.data.token",
                    DefaultValue = "default-token-value",
                },
                new()
                {
                    VariableName = "sessionId",
                    ExtractFrom = "ResponseHeader",
                    HeaderName = "X-Session-Id",
                },
                new()
                {
                    VariableName = "statusCode",
                    ExtractFrom = "Status",
                },
            },
        };

        SetupLlmSuggestions(scenarioWithVariables);

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = false,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = true,
            UserId = DefaultUserId,
        };

        // Act
        var result = await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert
        result.TestCases.Should().HaveCount(1);
        var testCase = result.TestCases[0];
        testCase.Variables.Should().HaveCount(3);

        var variables = testCase.Variables.ToList();

        // Variable 1: ResponseBody extraction
        var v1 = variables[0];
        v1.VariableName.Should().Be("authToken");
        v1.ExtractFrom.Should().Be(ExtractFrom.ResponseBody);
        v1.JsonPath.Should().Be("$.data.token");
        v1.DefaultValue.Should().Be("default-token-value");
        v1.TestCaseId.Should().Be(testCase.Id);
        v1.Id.Should().NotBeEmpty();

        // Variable 2: ResponseHeader extraction
        var v2 = variables[1];
        v2.VariableName.Should().Be("sessionId");
        v2.ExtractFrom.Should().Be(ExtractFrom.ResponseHeader);
        v2.HeaderName.Should().Be("X-Session-Id");

        // Variable 3: Status extraction
        var v3 = variables[2];
        v3.VariableName.Should().Be("statusCode");
        v3.ExtractFrom.Should().Be(ExtractFrom.Status);
    }

    [Fact]
    public async Task GenerateAsync_Should_PassParameterDetails_ToLlmContext()
    {
        // Arrange
        var suite = CreateSuite();
        var endpoints = CreateOrderedEndpoints();
        SetupEndpointMetadata(endpoints);
        SetupParameterDetails(Endpoint1Id,
            CreateBodyParameter("email", "string", "email"),
            CreateBodyParameter("password", "string", null));

        _bodyMutationEngineMock.Setup(x => x.GenerateMutations(It.IsAny<BodyMutationContext>()))
            .Returns(Array.Empty<BodyMutation>());

        LlmScenarioSuggestionContext capturedContext = null;
        _llmSuggesterMock.Setup(x => x.SuggestScenariosAsync(
                It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()))
            .Callback<LlmScenarioSuggestionContext, CancellationToken>((ctx, _) =>
            {
                capturedContext = ctx;
            })
            .ReturnsAsync(new LlmScenarioSuggestionResult
            {
                Scenarios = Array.Empty<LlmSuggestedScenario>(),
                LlmModel = "gpt-4o",
                TokensUsed = 0,
            });

        var options = new BoundaryNegativeOptions
        {
            IncludePathMutations = false,
            IncludeBodyMutations = true, // Need this so parameter details are fetched
            IncludeLlmSuggestions = true,
            UserId = DefaultUserId,
        };

        // Act
        await _generator.GenerateAsync(suite, endpoints, DefaultSpecId, options);

        // Assert — EndpointParameterDetails should be passed to LLM context
        capturedContext.Should().NotBeNull();
        capturedContext.EndpointParameterDetails.Should().NotBeNull();
        capturedContext.EndpointParameterDetails.Should().ContainKey(Endpoint1Id);
        capturedContext.EndpointParameterDetails[Endpoint1Id].Parameters.Should().HaveCount(2);
    }

    #region Helpers

    private static TestSuite CreateSuite()
    {
        return new TestSuite
        {
            Id = DefaultSuiteId,
            CreatedById = DefaultUserId,
            Name = "Boundary/Negative Test Suite",
            Status = TestSuiteStatus.Ready,
            ApprovalStatus = ApprovalStatus.Approved,
            Version = 1,
            EndpointBusinessContexts = new Dictionary<Guid, string>(),
        };
    }

    private static IReadOnlyList<ApiOrderItemModel> CreateOrderedEndpoints()
    {
        return new List<ApiOrderItemModel>
        {
            new()
            {
                EndpointId = Endpoint1Id,
                HttpMethod = "POST",
                Path = "/api/users",
                OrderIndex = 0,
            },
        };
    }

    private void SetupEndpointMetadata(IReadOnlyList<ApiOrderItemModel> endpoints)
    {
        var metadata = endpoints.Select(e => new ApiEndpointMetadataDto
        {
            EndpointId = e.EndpointId,
            HttpMethod = e.HttpMethod,
            Path = e.Path,
            OperationId = $"op_{e.HttpMethod}_{e.Path}",
            ParameterSchemaPayloads = new List<string> { "{\"type\":\"object\"}" },
            ResponseSchemaPayloads = new List<string>(),
        }).ToList();

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(metadata);
    }

    private static ParameterDetailDto CreatePathParameter(string name, string dataType, string format)
    {
        return new ParameterDetailDto
        {
            ParameterId = Guid.NewGuid(),
            Name = name,
            Location = "Path",
            DataType = dataType,
            Format = format,
            IsRequired = true,
            DefaultValue = "1",
        };
    }

    private static ParameterDetailDto CreateBodyParameter(string name, string dataType, string format)
    {
        return new ParameterDetailDto
        {
            ParameterId = Guid.NewGuid(),
            Name = name,
            Location = "Body",
            DataType = dataType,
            Format = format,
            IsRequired = true,
        };
    }

    private void SetupParameterDetails(Guid endpointId, params ParameterDetailDto[] parameters)
    {
        var details = new List<EndpointParameterDetailDto>
        {
            new()
            {
                EndpointId = endpointId,
                EndpointPath = "/api/users",
                EndpointHttpMethod = "POST",
                Parameters = parameters.ToList(),
            },
        };

        _parameterDetailServiceMock.Setup(x => x.GetParameterDetailsAsync(
                It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
    }

    private void SetupPathMutations(string paramName, params PathParameterMutationDto[] mutations)
    {
        _pathMutationServiceMock.Setup(x => x.GenerateMutations(
                paramName, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(mutations.ToList());
    }

    private void SetupBodyMutations(params BodyMutation[] mutations)
    {
        _bodyMutationEngineMock.Setup(x => x.GenerateMutations(It.IsAny<BodyMutationContext>()))
            .Returns(mutations.ToList());
    }

    private void SetupLlmSuggestions(params LlmSuggestedScenario[] scenarios)
    {
        _llmSuggesterMock.Setup(x => x.SuggestScenariosAsync(
                It.IsAny<LlmScenarioSuggestionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmScenarioSuggestionResult
            {
                Scenarios = scenarios.ToList(),
                LlmModel = "gpt-4o",
                TokensUsed = 500,
            });
    }

    #endregion
}
