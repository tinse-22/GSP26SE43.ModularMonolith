using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Caching.Distributed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class TestFailureReadGatewayServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Mock<IRepository<TestRun, Guid>> _runRepositoryMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ITestExecutionReadGatewayService> _executionReadGatewayMock;
    private readonly TestFailureReadGatewayService _service;

    private readonly Guid _suiteId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _apiSpecId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public TestFailureReadGatewayServiceTests()
    {
        _runRepositoryMock = new Mock<IRepository<TestRun, Guid>>();
        _cacheMock = new Mock<IDistributedCache>();
        _executionReadGatewayMock = new Mock<ITestExecutionReadGatewayService>();

        _service = new TestFailureReadGatewayService(
            _runRepositoryMock.Object,
            _cacheMock.Object,
            _executionReadGatewayMock.Object);
    }

    [Fact]
    public async Task GetFailureExplanationContextAsync_RunNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();

        SetupSuiteAccessContext();
        SetupRunRepository(null);

        // Act
        var act = () => _service.GetFailureExplanationContextAsync(_suiteId, runId, testCaseId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetFailureExplanationContextAsync_MissingCachedResults_ShouldThrowRunResultsExpiredConflict()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var run = CreateRun(runId, resultsExpireAt: DateTimeOffset.UtcNow.AddHours(1));

        SetupSuiteAccessContext();
        SetupRunRepository(run);
        _cacheMock.Setup(x => x.GetAsync(run.RedisKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null);

        // Act
        var act = () => _service.GetFailureExplanationContextAsync(_suiteId, runId, testCaseId);

        // Assert
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("RUN_RESULTS_EXPIRED");
    }

    [Fact]
    public async Task GetFailureExplanationContextAsync_MissingRedisKey_ShouldThrowRunResultsExpiredConflict()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var run = CreateRun(runId, resultsExpireAt: DateTimeOffset.UtcNow.AddHours(1));
        run.RedisKey = null;

        SetupSuiteAccessContext();
        SetupRunRepository(run);

        // Act
        var act = () => _service.GetFailureExplanationContextAsync(_suiteId, runId, testCaseId);

        // Assert
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("RUN_RESULTS_EXPIRED");
        _cacheMock.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _executionReadGatewayMock.Verify(x => x.GetExecutionContextAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFailureExplanationContextAsync_ExpiredCachedResults_ShouldThrowRunResultsExpiredConflict()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var run = CreateRun(runId, resultsExpireAt: DateTimeOffset.UtcNow.AddHours(-1));

        SetupSuiteAccessContext();
        SetupRunRepository(run);

        // Act
        var act = () => _service.GetFailureExplanationContextAsync(_suiteId, runId, testCaseId);

        // Assert
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("RUN_RESULTS_EXPIRED");
        _executionReadGatewayMock.Verify(x => x.GetExecutionContextAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFailureExplanationContextAsync_CaseNotFoundInCachedResults_ShouldThrowNotFoundException()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var requestedCaseId = Guid.NewGuid();
        var run = CreateRun(runId, resultsExpireAt: DateTimeOffset.UtcNow.AddHours(1));
        var otherCase = CreateCachedCaseResult(Guid.NewGuid(), status: "Failed");

        SetupSuiteAccessContext();
        SetupRunRepository(run);
        SetupCachedRunResults(run.RedisKey, new TestRunResultModel
        {
            ExecutedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ResolvedEnvironmentName = "QA",
            Cases = new List<TestCaseRunResultModel> { otherCase },
        });

        // Act
        var act = () => _service.GetFailureExplanationContextAsync(_suiteId, runId, requestedCaseId);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _executionReadGatewayMock.Verify(x => x.GetExecutionContextAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFailureExplanationContextAsync_CaseStatusPassed_ShouldThrowTestCaseNotFailedConflict()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var run = CreateRun(runId, resultsExpireAt: DateTimeOffset.UtcNow.AddHours(1));

        SetupSuiteAccessContext();
        SetupRunRepository(run);
        SetupCachedRunResults(run.RedisKey, new TestRunResultModel
        {
            ExecutedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            ResolvedEnvironmentName = "QA",
            Cases = new List<TestCaseRunResultModel>
            {
                CreateCachedCaseResult(testCaseId, status: "Passed"),
            },
        });

        // Act
        var act = () => _service.GetFailureExplanationContextAsync(_suiteId, runId, testCaseId);

        // Assert
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("TEST_CASE_NOT_FAILED");
        _executionReadGatewayMock.Verify(x => x.GetExecutionContextAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetFailureExplanationContextAsync_ValidFailedCase_ShouldReturnOriginalDefinitionAndActualResult()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var executedAt = DateTimeOffset.UtcNow.AddMinutes(-12);
        var run = CreateRun(runId, triggeredById: Guid.NewGuid(), resultsExpireAt: DateTimeOffset.UtcNow.AddHours(1));
        var cachedCase = CreateCachedCaseResult(testCaseId, status: "Failed", endpointId: endpointId);
        var definition = CreateExecutionTestCase(testCaseId, endpointId);

        SetupSuiteAccessContext();
        SetupRunRepository(run);
        SetupCachedRunResults(run.RedisKey, new TestRunResultModel
        {
            ExecutedAt = executedAt,
            ResolvedEnvironmentName = "Staging",
            Cases = new List<TestCaseRunResultModel> { cachedCase },
        });
        SetupExecutionContext(definition);

        // Act
        var result = await _service.GetFailureExplanationContextAsync(_suiteId, runId, testCaseId);

        // Assert
        result.TestSuiteId.Should().Be(_suiteId);
        result.ProjectId.Should().Be(_projectId);
        result.ApiSpecId.Should().Be(_apiSpecId);
        result.CreatedById.Should().Be(_ownerId);
        result.TestRunId.Should().Be(runId);
        result.RunNumber.Should().Be(run.RunNumber);
        result.TriggeredById.Should().Be(run.TriggeredById);
        result.ResolvedEnvironmentName.Should().Be("Staging");
        result.ExecutedAt.Should().Be(executedAt);

        result.Definition.Should().NotBeNull();
        result.Definition.TestCaseId.Should().Be(testCaseId);
        result.Definition.EndpointId.Should().Be(endpointId);
        result.Definition.Name.Should().Be(definition.Name);
        result.Definition.Request.Should().NotBeNull();
        result.Definition.Request.HttpMethod.Should().Be("POST");
        result.Definition.Expectation.Should().NotBeNull();
        result.Definition.Expectation.ExpectedStatus.Should().Be("201");
        result.Definition.DependencyIds.Should().Equal(definition.DependencyIds);

        result.ActualResult.Should().NotBeNull();
        result.ActualResult.Status.Should().Be("Failed");
        result.ActualResult.HttpStatusCode.Should().Be(500);
        result.ActualResult.ResolvedUrl.Should().Be("/api/orders");
        result.ActualResult.ResponseBodyPreview.Should().Be("{\"error\":\"boom\"}");
        result.ActualResult.FailureReasons.Should().ContainSingle();
        result.ActualResult.FailureReasons[0].Code.Should().Be("STATUS_CODE_MISMATCH");
        result.ActualResult.ExtractedVariables.Should().ContainKey("traceId");
        result.ActualResult.DependencyIds.Should().Equal(cachedCase.DependencyIds);
        result.ActualResult.SkippedBecauseDependencyIds.Should().BeEmpty();

        _executionReadGatewayMock.Verify(x => x.GetExecutionContextAsync(
            _suiteId,
            It.Is<IReadOnlyCollection<Guid>>(selectedIds => selectedIds == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupSuiteAccessContext()
    {
        _executionReadGatewayMock.Setup(x => x.GetSuiteAccessContextAsync(_suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteAccessContextDto
            {
                TestSuiteId = _suiteId,
                ProjectId = _projectId,
                ApiSpecId = _apiSpecId,
                CreatedById = _ownerId,
                Status = "Ready",
                Name = "Failure Suite",
            });
    }

    private void SetupRunRepository(TestRun run)
    {
        _runRepositoryMock.Setup(x => x.GetQueryableSet())
            .Returns(run != null
                ? new[] { run }.AsQueryable()
                : Array.Empty<TestRun>().AsQueryable());

        _runRepositoryMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestRun>>()))
            .ReturnsAsync(run);
    }

    private void SetupCachedRunResults(string redisKey, TestRunResultModel result)
    {
        var payload = JsonSerializer.Serialize(result, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(payload);

        _cacheMock.Setup(x => x.GetAsync(redisKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);
    }

    private void SetupExecutionContext(ExecutionTestCaseDto definition)
    {
        _executionReadGatewayMock.Setup(x => x.GetExecutionContextAsync(_suiteId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteExecutionContextDto
            {
                Suite = new TestSuiteAccessContextDto
                {
                    TestSuiteId = _suiteId,
                    ProjectId = _projectId,
                    ApiSpecId = _apiSpecId,
                    CreatedById = _ownerId,
                    Status = "Ready",
                    Name = "Failure Suite",
                },
                OrderedTestCases = new List<ExecutionTestCaseDto> { definition },
                OrderedEndpointIds = new List<Guid> { definition.EndpointId ?? Guid.Empty },
            });
    }

    private TestRun CreateRun(
        Guid runId,
        Guid? triggeredById = null,
        DateTimeOffset? resultsExpireAt = null)
    {
        return new TestRun
        {
            Id = runId,
            TestSuiteId = _suiteId,
            EnvironmentId = Guid.NewGuid(),
            TriggeredById = triggeredById ?? Guid.NewGuid(),
            RunNumber = 7,
            Status = TestRunStatus.Failed,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            TotalTests = 3,
            PassedCount = 1,
            FailedCount = 1,
            SkippedCount = 1,
            DurationMs = 1500,
            RedisKey = $"testrun:{runId}:results",
            ResultsExpireAt = resultsExpireAt,
        };
    }

    private static TestCaseRunResultModel CreateCachedCaseResult(Guid testCaseId, string status, Guid? endpointId = null)
    {
        return new TestCaseRunResultModel
        {
            TestCaseId = testCaseId,
            EndpointId = endpointId,
            Name = "Create order",
            OrderIndex = 2,
            Status = status,
            HttpStatusCode = 500,
            DurationMs = 321,
            ResolvedUrl = "/api/orders",
            RequestHeaders = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
            },
            ResponseHeaders = new Dictionary<string, string>
            {
                ["X-Trace-Id"] = "trace-001",
            },
            ResponseBodyPreview = "{\"error\":\"boom\"}",
            FailureReasons = new List<ValidationFailureModel>
            {
                new()
                {
                    Code = "STATUS_CODE_MISMATCH",
                    Message = "Expected 201 but got 500.",
                    Target = "statusCode",
                    Expected = "201",
                    Actual = "500",
                },
            },
            ExtractedVariables = new Dictionary<string, string>
            {
                ["traceId"] = "trace-001",
            },
            DependencyIds = new List<Guid> { Guid.NewGuid() },
            SkippedBecauseDependencyIds = new List<Guid>(),
            StatusCodeMatched = false,
            SchemaMatched = false,
            HeaderChecksPassed = true,
            BodyContainsPassed = false,
            BodyNotContainsPassed = true,
            JsonPathChecksPassed = false,
            ResponseTimePassed = true,
        };
    }

    private static ExecutionTestCaseDto CreateExecutionTestCase(Guid testCaseId, Guid endpointId)
    {
        return new ExecutionTestCaseDto
        {
            TestCaseId = testCaseId,
            EndpointId = endpointId,
            Name = "Create order",
            Description = "Create order should return 201.",
            TestType = "HappyPath",
            OrderIndex = 2,
            DependencyIds = new[] { Guid.NewGuid() },
            Request = new ExecutionTestCaseRequestDto
            {
                HttpMethod = "POST",
                Url = "/api/orders",
                Headers = "{\"Content-Type\":\"application/json\"}",
                PathParams = "{}",
                QueryParams = "{}",
                BodyType = "Json",
                Body = "{\"sku\":\"A-001\"}",
                Timeout = 30000,
            },
            Expectation = new ExecutionTestCaseExpectationDto
            {
                ExpectedStatus = "201",
                ResponseSchema = "{\"type\":\"object\"}",
                HeaderChecks = "{\"Content-Type\":\"application/json\"}",
                BodyContains = "[\"id\"]",
                BodyNotContains = "[]",
                JsonPathChecks = "{\"$.id\":\"exists\"}",
                MaxResponseTime = 1000,
            },
        };
    }
}
