using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class TestRunReportReadGatewayServiceTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Mock<IRepository<TestRun, Guid>> _runRepositoryMock;
    private readonly Mock<IRepository<TestCaseResult, Guid>> _resultRepositoryMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ITestExecutionReadGatewayService> _executionReadGatewayMock;
    private readonly TestRunReportReadGatewayService _service;

    private readonly Guid _suiteId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _apiSpecId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public TestRunReportReadGatewayServiceTests()
    {
        _runRepositoryMock = new Mock<IRepository<TestRun, Guid>>();
        _resultRepositoryMock = new Mock<IRepository<TestCaseResult, Guid>>();
        _cacheMock = new Mock<IDistributedCache>();
        _executionReadGatewayMock = new Mock<ITestExecutionReadGatewayService>();
        _resultRepositoryMock.Setup(x => x.GetQueryableSet())
            .Returns(Array.Empty<TestCaseResult>().AsQueryable());
        _resultRepositoryMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseResult>>()))
            .ReturnsAsync((IQueryable<TestCaseResult> query) => query.ToList());

        _service = new TestRunReportReadGatewayService(
            _runRepositoryMock.Object,
            _resultRepositoryMock.Object,
            _cacheMock.Object,
            _executionReadGatewayMock.Object,
            new Mock<ILogger<TestRunReportReadGatewayService>>().Object);
    }

    [Fact]
    public async Task GetReportContextAsync_RunNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        SetupSuiteAccessContext();
        SetupRunRepository();

        // Act
        var act = () => _service.GetReportContextAsync(_suiteId, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetReportContextAsync_RunNotFinished_ShouldThrowReportRunNotReadyConflict()
    {
        // Arrange
        var run = CreateRun(Guid.NewGuid(), runNumber: 4, status: TestRunStatus.Running);

        SetupSuiteAccessContext();
        SetupRunRepository(run);

        // Act
        var act = () => _service.GetReportContextAsync(_suiteId, run.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("REPORT_RUN_NOT_READY");
        _cacheMock.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _executionReadGatewayMock.Verify(x => x.GetExecutionContextAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetReportContextAsync_MissingCachedResults_ShouldThrowRunResultsExpiredConflict()
    {
        // Arrange
        var run = CreateRun(Guid.NewGuid(), runNumber: 4, resultsExpireAt: DateTimeOffset.UtcNow.AddHours(1));

        SetupSuiteAccessContext();
        SetupRunRepository(run);
        _cacheMock.Setup(x => x.GetAsync(run.RedisKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null);

        // Act
        var act = () => _service.GetReportContextAsync(_suiteId, run.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("RUN_RESULTS_EXPIRED");
        _executionReadGatewayMock.Verify(x => x.GetExecutionContextAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetReportContextAsync_ExpiredCachedResults_ShouldThrowRunResultsExpiredConflict()
    {
        // Arrange
        var run = CreateRun(Guid.NewGuid(), runNumber: 4, resultsExpireAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        SetupSuiteAccessContext();
        SetupRunRepository(run);

        // Act
        var act = () => _service.GetReportContextAsync(_suiteId, run.Id);

        // Assert
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("RUN_RESULTS_EXPIRED");
        _cacheMock.Verify(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _executionReadGatewayMock.Verify(x => x.GetExecutionContextAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetReportContextAsync_ValidRun_ShouldReturnDefinitionsResultsAndRecentHistory()
    {
        // Arrange
        var currentRun = CreateRun(Guid.NewGuid(), runNumber: 10, status: TestRunStatus.Failed, triggeredById: Guid.NewGuid(), resultsExpireAt: DateTimeOffset.UtcNow.AddHours(1));
        var olderRun1 = CreateRun(Guid.NewGuid(), runNumber: 9, status: TestRunStatus.Completed, triggeredById: Guid.NewGuid());
        var olderRun2 = CreateRun(Guid.NewGuid(), runNumber: 8, status: TestRunStatus.Failed, triggeredById: Guid.NewGuid());
        var muchOlderRun = CreateRun(Guid.NewGuid(), runNumber: 7, status: TestRunStatus.Completed, triggeredById: Guid.NewGuid());
        var testCaseId1 = Guid.NewGuid();
        var testCaseId2 = Guid.NewGuid();
        var endpointId1 = Guid.NewGuid();
        var endpointId2 = Guid.NewGuid();
        var dependencyId = Guid.NewGuid();
        var executedAt = DateTimeOffset.UtcNow.AddMinutes(-20);

        SetupSuiteAccessContext();
        SetupRunRepository(currentRun, olderRun1, olderRun2, muchOlderRun);
        SetupCachedRunResults(currentRun.RedisKey, new TestRunResultModel
        {
            ExecutedAt = executedAt,
            ResolvedEnvironmentName = "Staging",
            Cases = new List<TestCaseRunResultModel>
            {
                CreateCachedCaseResult(testCaseId2, "Failed", endpointId2, orderIndex: 2),
                CreateCachedCaseResult(testCaseId1, "Passed", endpointId1, orderIndex: 1),
            },
        });
        SetupExecutionContext(
            CreateExecutionTestCase(testCaseId1, endpointId1, 1, dependencyId),
            CreateExecutionTestCase(testCaseId2, endpointId2, 2, Guid.NewGuid()));

        // Act
        var result = await _service.GetReportContextAsync(_suiteId, currentRun.Id, recentHistoryLimit: 2);

        // Assert
        result.TestSuiteId.Should().Be(_suiteId);
        result.ProjectId.Should().Be(_projectId);
        result.ApiSpecId.Should().Be(_apiSpecId);
        result.CreatedById.Should().Be(_ownerId);
        result.SuiteName.Should().Be("Report Suite");

        result.Run.Should().NotBeNull();
        result.Run.TestRunId.Should().Be(currentRun.Id);
        result.Run.RunNumber.Should().Be(10);
        result.Run.EnvironmentId.Should().Be(currentRun.EnvironmentId);
        result.Run.TriggeredById.Should().Be(currentRun.TriggeredById);
        result.Run.Status.Should().Be("Failed");
        result.Run.TotalTests.Should().Be(currentRun.TotalTests);
        result.Run.FailedCount.Should().Be(currentRun.FailedCount);
        result.Run.ResolvedEnvironmentName.Should().Be("Staging");
        result.Run.ExecutedAt.Should().Be(executedAt);

        result.OrderedEndpointIds.Should().Equal(endpointId1, endpointId2);

        result.Definitions.Should().HaveCount(2);
        result.Definitions[0].OrderIndex.Should().Be(1);
        result.Definitions[0].DependencyIds.Should().Equal(dependencyId);
        result.Definitions[0].Request.HttpMethod.Should().Be("POST");
        result.Definitions[0].Expectation.ExpectedStatus.Should().Be("201");
        result.Definitions[1].OrderIndex.Should().Be(2);

        result.Results.Should().HaveCount(2);
        result.Results[0].OrderIndex.Should().Be(1);
        result.Results[0].Status.Should().Be("Passed");
        result.Results[1].OrderIndex.Should().Be(2);
        result.Results[1].Status.Should().Be("Failed");
        result.Results[1].FailureReasons.Should().ContainSingle();
        result.Results[1].ExtractedVariables.Should().ContainKey("traceId");

        result.RecentRuns.Should().HaveCount(2);
        result.RecentRuns.Select(x => x.RunNumber).Should().Equal(9, 8);
        result.RecentRuns.Select(x => x.TestRunId).Should().NotContain(currentRun.Id);

        _executionReadGatewayMock.Verify(x => x.GetSuiteAccessContextAsync(_suiteId, It.IsAny<CancellationToken>()), Times.Once);
        _executionReadGatewayMock.Verify(x => x.GetExecutionContextAsync(
            _suiteId,
            It.Is<IReadOnlyCollection<Guid>>(selectedIds => selectedIds == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetReportContextAsync_HistoryLimitBelowMinimum_ShouldClampToOne()
    {
        // Arrange
        var currentRun = CreateRun(Guid.NewGuid(), runNumber: 10, status: TestRunStatus.Completed, resultsExpireAt: DateTimeOffset.UtcNow.AddHours(1));
        var olderRun1 = CreateRun(Guid.NewGuid(), runNumber: 9, status: TestRunStatus.Completed);
        var olderRun2 = CreateRun(Guid.NewGuid(), runNumber: 8, status: TestRunStatus.Failed);
        var testCaseId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        SetupSuiteAccessContext();
        SetupRunRepository(currentRun, olderRun1, olderRun2);
        SetupCachedRunResults(currentRun.RedisKey, new TestRunResultModel
        {
            ExecutedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ResolvedEnvironmentName = "QA",
            Cases = new List<TestCaseRunResultModel>
            {
                CreateCachedCaseResult(testCaseId, "Passed", endpointId, orderIndex: 1),
            },
        });
        SetupExecutionContext(CreateExecutionTestCase(testCaseId, endpointId, 1, Guid.NewGuid()));

        // Act
        var result = await _service.GetReportContextAsync(_suiteId, currentRun.Id, recentHistoryLimit: 0);

        // Assert
        result.RecentRuns.Should().ContainSingle();
        result.RecentRuns[0].RunNumber.Should().Be(9);
    }

    [Fact]
    public async Task GetReportContextAsync_CacheThrows_ShouldFallBackToDatabase()
    {
        // Arrange
        var currentRun = CreateRun(Guid.NewGuid(), runNumber: 10, status: TestRunStatus.Failed, resultsExpireAt: DateTimeOffset.UtcNow.AddHours(1));
        var testCaseId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        SetupSuiteAccessContext();
        SetupRunRepository(currentRun);
        _cacheMock.Setup(x => x.GetAsync(currentRun.RedisKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis offline"));
        _resultRepositoryMock.Setup(x => x.GetQueryableSet())
            .Returns(new[]
            {
                new TestCaseResult
                {
                    Id = Guid.NewGuid(),
                    TestRunId = currentRun.Id,
                    TestCaseId = testCaseId,
                    EndpointId = endpointId,
                    Name = "Recovered case",
                    OrderIndex = 1,
                    Status = "Failed",
                    HttpStatusCode = 500,
                    DurationMs = 250,
                    ResolvedUrl = "/api/orders",
                    RequestHeaders = "{\"Content-Type\":\"application/json\"}",
                    ResponseHeaders = "{\"X-Trace-Id\":\"trace-001\"}",
                    ResponseBodyPreview = "{\"error\":\"boom\"}",
                    FailureReasons = "[{\"code\":\"STATUS_CODE_MISMATCH\",\"message\":\"Expected 201 but got 500.\",\"target\":\"statusCode\",\"expected\":\"201\",\"actual\":\"500\"}]",
                    ExtractedVariables = "{\"traceId\":\"trace-001\"}",
                    DependencyIds = "[]",
                    SkippedBecauseDependencyIds = "[]",
                    StatusCodeMatched = false,
                    SchemaMatched = false,
                    HeaderChecksPassed = true,
                    BodyContainsPassed = false,
                    BodyNotContainsPassed = true,
                    JsonPathChecksPassed = false,
                    ResponseTimePassed = true,
                },
            }.AsQueryable());
        SetupExecutionContext(CreateExecutionTestCase(testCaseId, endpointId, 1, Guid.NewGuid()));

        // Act
        var result = await _service.GetReportContextAsync(_suiteId, currentRun.Id);

        // Assert
        result.Results.Should().ContainSingle();
        result.Results[0].Name.Should().Be("Recovered case");
        result.Results[0].Status.Should().Be("Failed");
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
                Name = "Report Suite",
            });
    }

    private void SetupRunRepository(params TestRun[] runs)
    {
        var queryable = runs.AsQueryable();

        _runRepositoryMock.Setup(x => x.GetQueryableSet())
            .Returns(queryable);

        _runRepositoryMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestRun>>()))
            .ReturnsAsync((IQueryable<TestRun> q) => q.FirstOrDefault());

        _runRepositoryMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestRun>>()))
            .ReturnsAsync((IQueryable<TestRun> q) => q.ToList());
    }

    private void SetupCachedRunResults(string redisKey, TestRunResultModel result)
    {
        var payload = JsonSerializer.Serialize(result, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(payload);

        _cacheMock.Setup(x => x.GetAsync(redisKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bytes);
    }

    private void SetupExecutionContext(params ExecutionTestCaseDto[] definitions)
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
                    Name = "Report Suite",
                },
                OrderedTestCases = definitions,
                OrderedEndpointIds = definitions
                    .Where(x => x.EndpointId.HasValue)
                    .Select(x => x.EndpointId.Value)
                    .ToArray(),
            });
    }

    private TestRun CreateRun(
        Guid runId,
        int runNumber,
        TestRunStatus status = TestRunStatus.Failed,
        Guid? triggeredById = null,
        DateTimeOffset? resultsExpireAt = null)
    {
        return new TestRun
        {
            Id = runId,
            TestSuiteId = _suiteId,
            EnvironmentId = Guid.NewGuid(),
            TriggeredById = triggeredById ?? Guid.NewGuid(),
            RunNumber = runNumber,
            Status = status,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-15),
            CompletedAt = status is TestRunStatus.Completed or TestRunStatus.Failed
                ? DateTimeOffset.UtcNow.AddMinutes(-10)
                : null,
            TotalTests = 3,
            PassedCount = status == TestRunStatus.Completed ? 3 : 1,
            FailedCount = status == TestRunStatus.Failed ? 1 : 0,
            SkippedCount = status == TestRunStatus.Failed ? 1 : 0,
            DurationMs = 1500 + runNumber,
            RedisKey = $"testrun:{runId}:results",
            ResultsExpireAt = resultsExpireAt,
        };
    }

    private static TestCaseRunResultModel CreateCachedCaseResult(
        Guid testCaseId,
        string status,
        Guid endpointId,
        int orderIndex)
    {
        return new TestCaseRunResultModel
        {
            TestCaseId = testCaseId,
            EndpointId = endpointId,
            Name = $"Case {orderIndex}",
            OrderIndex = orderIndex,
            Status = status,
            HttpStatusCode = status == "Failed" ? 500 : 201,
            DurationMs = 321 + orderIndex,
            ResolvedUrl = "/api/orders",
            RequestHeaders = new Dictionary<string, string>
            {
                ["Content-Type"] = "application/json",
            },
            ResponseHeaders = new Dictionary<string, string>
            {
                ["X-Trace-Id"] = "trace-001",
            },
            ResponseBodyPreview = status == "Failed" ? "{\"error\":\"boom\"}" : "{\"id\":1}",
            FailureReasons = status == "Failed"
                ? new List<ValidationFailureModel>
                {
                    new()
                    {
                        Code = "STATUS_CODE_MISMATCH",
                        Message = "Expected 201 but got 500.",
                        Target = "statusCode",
                        Expected = "201",
                        Actual = "500",
                    },
                }
                : new List<ValidationFailureModel>(),
            ExtractedVariables = new Dictionary<string, string>
            {
                ["traceId"] = "trace-001",
            },
            DependencyIds = new List<Guid> { Guid.NewGuid() },
            SkippedBecauseDependencyIds = new List<Guid>(),
            StatusCodeMatched = status != "Failed",
            SchemaMatched = status != "Failed",
            HeaderChecksPassed = true,
            BodyContainsPassed = status != "Failed",
            BodyNotContainsPassed = true,
            JsonPathChecksPassed = status != "Failed",
            ResponseTimePassed = true,
        };
    }

    private static ExecutionTestCaseDto CreateExecutionTestCase(Guid testCaseId, Guid endpointId, int orderIndex, Guid dependencyId)
    {
        return new ExecutionTestCaseDto
        {
            TestCaseId = testCaseId,
            EndpointId = endpointId,
            Name = $"Definition {orderIndex}",
            Description = $"Definition {orderIndex} description",
            TestType = "HappyPath",
            OrderIndex = orderIndex,
            DependencyIds = new[] { dependencyId },
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
