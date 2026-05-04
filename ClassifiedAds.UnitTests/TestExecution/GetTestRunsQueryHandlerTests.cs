using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Queries;
using ClassifiedAds.UnitTests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class GetTestRunsQueryHandlerTests
{
    private readonly Mock<IRepository<TestRun, Guid>> _runRepoMock;
    private readonly Mock<ITestExecutionReadGatewayService> _gatewayMock;
    private readonly Mock<IRepository<ExecutionEnvironment, Guid>> _envRepoMock;
    private readonly GetTestRunsQueryHandler _handler;

    private readonly Guid _suiteId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public GetTestRunsQueryHandlerTests()
    {
        _runRepoMock = new Mock<IRepository<TestRun, Guid>>();
        _gatewayMock = new Mock<ITestExecutionReadGatewayService>();
        _envRepoMock = new Mock<IRepository<ExecutionEnvironment, Guid>>();

        _envRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new TestAsyncEnumerable<ExecutionEnvironment>(new System.Collections.Generic.List<ExecutionEnvironment>()));
        _envRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<ExecutionEnvironment>>()))
            .Returns<IQueryable<ExecutionEnvironment>>(query => Task.FromResult(query.ToList()));

        _handler = new GetTestRunsQueryHandler(_runRepoMock.Object, _gatewayMock.Object, _envRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WrongOwner_ShouldThrowValidation()
    {
        // Arrange
        SetupGateway(Guid.NewGuid());
        var query = new GetTestRunsQuery
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _ownerId,
        };

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_WithStatusFilter_ShouldFilterResults()
    {
        // Arrange
        SetupGateway();
        SetupRuns(new[]
        {
            CreateRun(1, TestRunStatus.Completed, DateTimeOffset.UtcNow.AddMinutes(-1)),
            CreateRun(2, TestRunStatus.Failed, DateTimeOffset.UtcNow.AddMinutes(-2)),
            CreateRun(3, TestRunStatus.Completed, DateTimeOffset.UtcNow.AddMinutes(-3)),
        });

        var query = new GetTestRunsQuery
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _ownerId,
            Status = TestRunStatus.Completed,
        };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.TotalItems.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(item => item.Status == nameof(TestRunStatus.Completed));
    }

    [Fact]
    public async Task HandleAsync_Paging_ShouldRespectPageSizeAndNumber()
    {
        // Arrange
        SetupGateway();
        SetupRuns(Enumerable.Range(1, 5)
            .Select(index => CreateRun(index, TestRunStatus.Completed, DateTimeOffset.UtcNow.AddMinutes(-index)))
            .ToList());

        var query = new GetTestRunsQuery
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _ownerId,
            PageNumber = 2,
            PageSize = 2,
        };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.TotalItems.Should().Be(5);
        result.Items.Should().HaveCount(2);
        result.Items.Select(item => item.RunNumber).Should().Equal(3, 4);
    }

    [Fact]
    public async Task HandleAsync_PageSizeClamp_ShouldNotExceed100()
    {
        // Arrange
        SetupGateway();
        SetupRuns(Enumerable.Range(1, 150)
            .Select(index => CreateRun(index, TestRunStatus.Completed, DateTimeOffset.UtcNow.AddMinutes(-index)))
            .ToList());

        var query = new GetTestRunsQuery
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _ownerId,
            PageNumber = 1,
            PageSize = 500,
        };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.PageSize.Should().Be(100);
        result.Items.Should().HaveCount(100);
    }

    private void SetupGateway(Guid? ownerId = null)
    {
        _gatewayMock
            .Setup(x => x.GetSuiteAccessContextAsync(_suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteAccessContextDto
            {
                TestSuiteId = _suiteId,
                CreatedById = ownerId ?? _ownerId,
                Status = "Ready",
            });
    }

    private void SetupRuns(IReadOnlyCollection<TestRun> runs)
    {
        var queryableRuns = new TestAsyncEnumerable<TestRun>(runs);

        _runRepoMock.Setup(x => x.GetQueryableSet()).Returns(queryableRuns);
        _runRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestRun>>()))
            .Returns<IQueryable<TestRun>>(query => Task.FromResult(query.ToList()));
    }

    private TestRun CreateRun(int runNumber, TestRunStatus status, DateTimeOffset createdDateTime)
    {
        return new TestRun
        {
            Id = Guid.NewGuid(),
            TestSuiteId = _suiteId,
            EnvironmentId = Guid.NewGuid(),
            TriggeredById = _ownerId,
            RunNumber = runNumber,
            Status = status,
            CreatedDateTime = createdDateTime,
        };
    }
}
