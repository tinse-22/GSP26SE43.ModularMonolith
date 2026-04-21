using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Queries;
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

public class GetTestRunResultsQueryHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Mock<IRepository<TestRun, Guid>> _runRepoMock;
    private readonly Mock<IRepository<TestCaseResult, Guid>> _resultRepoMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly Mock<ITestExecutionReadGatewayService> _gatewayMock;
    private readonly GetTestRunResultsQueryHandler _handler;

    private readonly Guid _ownerId = Guid.NewGuid();

    public GetTestRunResultsQueryHandlerTests()
    {
        _runRepoMock = new Mock<IRepository<TestRun, Guid>>();
        _resultRepoMock = new Mock<IRepository<TestCaseResult, Guid>>();
        _cacheMock = new Mock<IDistributedCache>();
        _gatewayMock = new Mock<ITestExecutionReadGatewayService>();

        _resultRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(Array.Empty<TestCaseResult>().AsQueryable());
        _resultRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseResult>>()))
            .ReturnsAsync((IQueryable<TestCaseResult> query) => query.ToList());

        _handler = new GetTestRunResultsQueryHandler(
            _runRepoMock.Object,
            _resultRepoMock.Object,
            _cacheMock.Object,
            _gatewayMock.Object,
            new Mock<ILogger<GetTestRunResultsQueryHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_RunNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = Guid.NewGuid(),
            CurrentUserId = _ownerId,
        };

        SetupGateway(suiteId, _ownerId);
        SetupRunRepository(null);

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_WrongOwner_ShouldThrowValidationException()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
        };

        SetupGateway(suiteId, _ownerId);

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_EmptyRedisKey_ShouldReturnUnavailable()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var run = CreateTestRun(runId, suiteId, resultsExpireAt: DateTimeOffset.UtcNow.AddDays(1));
        run.RedisKey = null;

        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = _ownerId,
        };

        SetupGateway(suiteId, _ownerId);
        SetupRunRepository(run);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.ResultsSource.Should().Be("unavailable");
        result.Cases.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_CacheExpired_ShouldReturnUnavailable()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var run = CreateTestRun(runId, suiteId, resultsExpireAt: DateTimeOffset.UtcNow.AddDays(-1));

        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = _ownerId,
        };

        SetupGateway(suiteId, _ownerId);
        SetupRunRepository(run);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.ResultsSource.Should().Be("unavailable");
        result.Cases.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_CacheMissing_ShouldReturnUnavailable()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var run = CreateTestRun(runId, suiteId, resultsExpireAt: DateTimeOffset.UtcNow.AddDays(1));

        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = _ownerId,
        };

        SetupGateway(suiteId, _ownerId);
        SetupRunRepository(run);

        // Cache returns null
        _cacheMock.Setup(x => x.GetAsync(run.RedisKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null!);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.ResultsSource.Should().Be("unavailable");
        result.Cases.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_CacheMissing_ShouldFallBackToDatabase()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var run = CreateTestRun(runId, suiteId, resultsExpireAt: DateTimeOffset.UtcNow.AddDays(1));

        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = _ownerId,
        };

        SetupGateway(suiteId, _ownerId);
        SetupRunRepository(run);

        _cacheMock.Setup(x => x.GetAsync(run.RedisKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null!);

        _resultRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new[]
            {
                CreatePersistedResult(runId, caseId),
            }.AsQueryable());

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.ResultsSource.Should().Be("database");
        result.Cases.Should().ContainSingle();
        result.Cases[0].TestCaseId.Should().Be(caseId);
        result.Cases[0].Name.Should().Be("Recovered case");
        result.Run.Id.Should().Be(runId);
    }

    [Fact]
    public async Task HandleAsync_ValidCache_ShouldReturnResults()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var run = CreateTestRun(runId, suiteId, resultsExpireAt: DateTimeOffset.UtcNow.AddDays(1));

        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = _ownerId,
        };

        SetupGateway(suiteId, _ownerId);
        SetupRunRepository(run);

        var cachedResult = new TestRunResultModel
        {
            ResultsSource = "cache",
            ExecutedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            ResolvedEnvironmentName = "Dev",
            Cases = new List<TestCaseRunResultModel>
            {
                new()
                {
                    TestCaseId = Guid.NewGuid(),
                    Name = "Test 1",
                    Status = "Passed",
                    DurationMs = 100,
                },
            },
        };

        var json = JsonSerializer.Serialize(cachedResult, JsonOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        _cacheMock.Setup(x => x.GetAsync(run.RedisKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(jsonBytes);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Cases.Should().HaveCount(1);
        result.Cases[0].Name.Should().Be("Test 1");
        result.Run.Should().NotBeNull();
        result.Run.Id.Should().Be(runId);
    }

    [Fact]
    public async Task HandleAsync_CacheThrows_ShouldReturnUnavailable()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var run = CreateTestRun(runId, suiteId, resultsExpireAt: DateTimeOffset.UtcNow.AddDays(1));
        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = _ownerId,
        };

        SetupGateway(suiteId, _ownerId);
        SetupRunRepository(run);

        _cacheMock.Setup(x => x.GetAsync(run.RedisKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis offline"));

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.ResultsSource.Should().Be("unavailable");
        result.Cases.Should().BeEmpty();
        result.Run.Id.Should().Be(runId);
    }

    #region Helpers

    private void SetupGateway(Guid suiteId, Guid ownerId)
    {
        _gatewayMock.Setup(x => x.GetSuiteAccessContextAsync(suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteAccessContextDto
            {
                TestSuiteId = suiteId,
                CreatedById = ownerId,
                Status = "Ready",
                Name = "Test Suite",
            });
    }

    private void SetupRunRepository(TestRun? run)
    {
        _runRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(run != null
                ? new List<TestRun> { run }.AsQueryable()
                : new List<TestRun>().AsQueryable());

        _runRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestRun>>()))
            .ReturnsAsync(run!);
    }

    private static TestRun CreateTestRun(
        Guid runId,
        Guid suiteId,
        string? redisKey = null,
        DateTimeOffset? resultsExpireAt = null)
    {
        return new TestRun
        {
            Id = runId,
            TestSuiteId = suiteId,
            EnvironmentId = Guid.NewGuid(),
            TriggeredById = Guid.NewGuid(),
            RunNumber = 1,
            Status = TestRunStatus.Completed,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            TotalTests = 5,
            PassedCount = 5,
            FailedCount = 0,
            SkippedCount = 0,
            DurationMs = 1000,
            RedisKey = redisKey ?? $"testrun:{runId}:results",
            ResultsExpireAt = resultsExpireAt,
        };
    }

    private static TestCaseResult CreatePersistedResult(Guid runId, Guid caseId)
    {
        return new TestCaseResult
        {
            Id = Guid.NewGuid(),
            TestRunId = runId,
            TestCaseId = caseId,
            EndpointId = Guid.NewGuid(),
            Name = "Recovered case",
            OrderIndex = 1,
            Status = "Failed",
            HttpStatusCode = 500,
            DurationMs = 150,
            ResolvedUrl = "/api/recovered",
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
        };
    }

    #endregion
}
