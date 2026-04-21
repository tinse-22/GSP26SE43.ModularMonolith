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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class TestResultCollectorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Mock<IRepository<TestRun, Guid>> _runRepoMock;
    private readonly Mock<IRepository<TestCaseResult, Guid>> _resultRepoMock;
    private readonly Mock<IDistributedCache> _cacheMock;
    private readonly TestResultCollector _collector;

    public TestResultCollectorTests()
    {
        _runRepoMock = new Mock<IRepository<TestRun, Guid>>();
        _resultRepoMock = new Mock<IRepository<TestCaseResult, Guid>>();
        _cacheMock = new Mock<IDistributedCache>();

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        _runRepoMock.Setup(x => x.UnitOfWork).Returns(unitOfWorkMock.Object);
        _runRepoMock.Setup(x => x.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _resultRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(Array.Empty<TestCaseResult>().AsQueryable());
        _resultRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseResult>>()))
            .ReturnsAsync((IQueryable<TestCaseResult> query) => query.ToList());
        _resultRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCaseResult>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _collector = new TestResultCollector(
            _runRepoMock.Object,
            _resultRepoMock.Object,
            _cacheMock.Object,
            new Mock<ILogger<TestResultCollector>>().Object);
    }

    #region Status Determination

    [Fact]
    public async Task CollectAsync_AllPassed_ShouldSetStatusCompleted()
    {
        // Arrange
        var run = CreateTestRun();
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed"),
            CreateCaseResult("Passed"),
            CreateCaseResult("Passed"),
        };

        // Act
        var result = await _collector.CollectAsync(run, caseResults, 7, "Dev");

        // Assert
        run.Status.Should().Be(TestRunStatus.Completed);
        run.PassedCount.Should().Be(3);
        run.FailedCount.Should().Be(0);
        run.TotalTests.Should().Be(3);
        result.Run.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task CollectAsync_HasFailures_ShouldSetStatusFailed()
    {
        // Arrange
        var run = CreateTestRun();
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed"),
            CreateCaseResult("Passed"),
            CreateCaseResult("Failed"),
        };

        // Act
        var result = await _collector.CollectAsync(run, caseResults, 7, "Dev");

        // Assert
        run.Status.Should().Be(TestRunStatus.Failed);
        run.PassedCount.Should().Be(2);
        run.FailedCount.Should().Be(1);
        result.Run.Status.Should().Be("Failed");
    }

    [Fact]
    public async Task CollectAsync_WithSkipped_ShouldCountCorrectly()
    {
        // Arrange
        var run = CreateTestRun();
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed"),
            CreateCaseResult("Failed"),
            CreateCaseResult("Skipped"),
        };

        // Act
        await _collector.CollectAsync(run, caseResults, 7, "Dev");

        // Assert
        run.PassedCount.Should().Be(1);
        run.FailedCount.Should().Be(1);
        run.SkippedCount.Should().Be(1);
        run.TotalTests.Should().Be(3);
        run.Status.Should().Be(TestRunStatus.Failed);
    }

    #endregion

    #region Body Truncation

    [Fact]
    public async Task CollectAsync_ShouldTruncateResponseBodyTo65536Chars()
    {
        // Arrange
        var run = CreateTestRun();
        var longBody = new string('x', 100_000);
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed", responseBody: longBody),
        };

        // Act
        var result = await _collector.CollectAsync(run, caseResults, 7, "Dev");

        // Assert
        result.Cases[0].ResponseBodyPreview.Should().HaveLength(65536);
    }

    [Fact]
    public async Task CollectAsync_ShortBody_ShouldNotTruncate()
    {
        // Arrange
        var run = CreateTestRun();
        var shortBody = "short response";
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed", responseBody: shortBody),
        };

        // Act
        var result = await _collector.CollectAsync(run, caseResults, 7, "Dev");

        // Assert
        result.Cases[0].ResponseBodyPreview.Should().Be(shortBody);
    }

    #endregion

    #region Sensitive Variable Masking

    [Fact]
    public async Task CollectAsync_ShouldMaskSensitiveExtractedVariables()
    {
        // Arrange
        var run = CreateTestRun();
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed", extractedVariables: new Dictionary<string, string>
            {
                ["auth_token"] = "abc123",
                ["client_secret"] = "my-secret-value",
                ["user_password"] = "p@ssw0rd",
                ["x_apikey"] = "key-12345",
                ["username"] = "alice",
                ["orderId"] = "12345",
            }),
        };

        // Act
        var result = await _collector.CollectAsync(run, caseResults, 7, "Dev");

        // Assert
        var vars = result.Cases[0].ExtractedVariables;
        vars["auth_token"].Should().Be("******");
        vars["client_secret"].Should().Be("******");
        vars["user_password"].Should().Be("******");
        vars["x_apikey"].Should().Be("******");
        vars["username"].Should().Be("alice");
        vars["orderId"].Should().Be("12345");
    }

    [Fact]
    public async Task CollectAsync_NullExtractedVariables_ShouldReturnEmptyDictionary()
    {
        // Arrange
        var run = CreateTestRun();
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed", extractedVariables: null),
        };

        // Act
        var result = await _collector.CollectAsync(run, caseResults, 7, "Dev");

        // Assert
        result.Cases[0].ExtractedVariables.Should().NotBeNull();
        result.Cases[0].ExtractedVariables.Should().BeEmpty();
    }

    #endregion

    #region Cache Write & DB Persist

    [Fact]
    public async Task CollectAsync_ShouldWriteToCache()
    {
        // Arrange
        var run = CreateTestRun();
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed"),
        };

        // Act
        await _collector.CollectAsync(run, caseResults, 7, "Dev");

        // Assert
        _cacheMock.Verify(
            x => x.SetAsync(
                run.RedisKey,
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CollectAsync_ShouldUpdateRunCountersAndPersist()
    {
        // Arrange
        var run = CreateTestRun();
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed", durationMs: 100),
            CreateCaseResult("Failed", durationMs: 200),
        };

        // Act
        await _collector.CollectAsync(run, caseResults, 7, "Dev");

        // Assert
        run.DurationMs.Should().Be(300);
        run.CompletedAt.Should().NotBeNull();
        run.ResultsExpireAt.Should().NotBeNull();

        _runRepoMock.Verify(x => x.UpdateAsync(run, It.IsAny<CancellationToken>()), Times.Once);
        _runRepoMock.Verify(x => x.UnitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _resultRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseResult>(), It.IsAny<CancellationToken>()), Times.Exactly(caseResults.Count));
    }

    [Fact]
    public async Task CollectAsync_CacheFail_ShouldReturnUnavailableAndPersistSummary()
    {
        // Arrange
        var run = CreateTestRun();
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed"),
        };

        _cacheMock.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Redis unavailable"));

        // Act
        var result = await _collector.CollectAsync(run, caseResults, 7, "Dev");

        // Assert
        result.ResultsSource.Should().Be("unavailable");
        run.Status.Should().Be(TestRunStatus.Completed);
        _runRepoMock.Verify(x => x.UpdateAsync(run, It.IsAny<CancellationToken>()), Times.Once);
        _runRepoMock.Verify(x => x.UnitOfWork.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region Result Model

    [Fact]
    public async Task CollectAsync_ShouldSetResultsSource()
    {
        // Arrange
        var run = CreateTestRun();
        var caseResults = new List<TestCaseExecutionResult>
        {
            CreateCaseResult("Passed"),
        };

        // Act
        var result = await _collector.CollectAsync(run, caseResults, 7, "Staging");

        // Assert
        result.ResultsSource.Should().Be("cache");
        result.ResolvedEnvironmentName.Should().Be("Staging");
    }

    #endregion

    #region Helpers

    private static TestRun CreateTestRun()
    {
        return new TestRun
        {
            Id = Guid.NewGuid(),
            TestSuiteId = Guid.NewGuid(),
            EnvironmentId = Guid.NewGuid(),
            TriggeredById = Guid.NewGuid(),
            RunNumber = 1,
            Status = TestRunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-10),
            RedisKey = $"testrun:{Guid.NewGuid()}:results",
        };
    }

    private static TestCaseExecutionResult CreateCaseResult(
        string status,
        long durationMs = 50,
        string? responseBody = null,
        Dictionary<string, string>? extractedVariables = null)
    {
        return new TestCaseExecutionResult
        {
            TestCaseId = Guid.NewGuid(),
            Name = $"Test case ({status})",
            OrderIndex = 0,
            Status = status,
            HttpStatusCode = 200,
            DurationMs = durationMs,
            ResolvedUrl = "https://api.example.com/test",
            ResponseBody = responseBody,
            ExtractedVariables = extractedVariables ?? new Dictionary<string, string>(),
        };
    }

    #endregion
}
