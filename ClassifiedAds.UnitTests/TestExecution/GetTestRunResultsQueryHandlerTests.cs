using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Queries;
using Microsoft.Extensions.Caching.Distributed;
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
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly GetTestRunResultsQueryHandler _handler;

    public GetTestRunResultsQueryHandlerTests()
    {
        _runRepoMock = new Mock<IRepository<TestRun, Guid>>();
        _cacheMock = new Mock<IDistributedCache>();

        _handler = new GetTestRunResultsQueryHandler(
            _runRepoMock.Object,
            _cacheMock.Object);
    }

    [Fact]
    public async Task HandleAsync_RunNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
        };

        SetupRunRepository(null);

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_EmptyRedisKey_ShouldThrowConflictException()
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
            CurrentUserId = Guid.NewGuid(),
        };

        SetupRunRepository(run);

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task HandleAsync_CacheExpired_ShouldThrowConflictException()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var run = CreateTestRun(runId, suiteId, resultsExpireAt: DateTimeOffset.UtcNow.AddDays(-1));

        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = Guid.NewGuid(),
        };

        SetupRunRepository(run);

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task HandleAsync_CacheMissing_ShouldThrowConflictException()
    {
        // Arrange
        var runId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var run = CreateTestRun(runId, suiteId, resultsExpireAt: DateTimeOffset.UtcNow.AddDays(1));

        var query = new GetTestRunResultsQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = Guid.NewGuid(),
        };

        SetupRunRepository(run);

        // Cache returns null
        _cacheMock.Setup(x => x.GetAsync(run.RedisKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[])null);

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
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
            CurrentUserId = Guid.NewGuid(),
        };

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

    #region Helpers

    private void SetupRunRepository(TestRun run)
    {
        _runRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(run != null
                ? new List<TestRun> { run }.AsQueryable()
                : new List<TestRun>().AsQueryable());

        _runRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestRun>>()))
            .ReturnsAsync(run);
    }

    private static TestRun CreateTestRun(
        Guid runId,
        Guid suiteId,
        string redisKey = null,
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

    #endregion
}
