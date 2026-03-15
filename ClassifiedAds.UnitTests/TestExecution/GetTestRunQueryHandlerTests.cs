using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class GetTestRunQueryHandlerTests
{
    private readonly Mock<IRepository<TestRun, Guid>> _runRepoMock;
    private readonly Mock<ITestExecutionReadGatewayService> _gatewayMock;
    private readonly GetTestRunQueryHandler _handler;

    private readonly Guid _suiteId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public GetTestRunQueryHandlerTests()
    {
        _runRepoMock = new Mock<IRepository<TestRun, Guid>>();
        _gatewayMock = new Mock<ITestExecutionReadGatewayService>();
        _handler = new GetTestRunQueryHandler(_runRepoMock.Object, _gatewayMock.Object);
    }

    [Fact]
    public async Task HandleAsync_WrongOwner_ShouldThrowValidation()
    {
        // Arrange
        SetupGateway(Guid.NewGuid());
        var query = new GetTestRunQuery
        {
            TestSuiteId = _suiteId,
            RunId = Guid.NewGuid(),
            CurrentUserId = _ownerId,
        };

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_RunNotFound_ShouldThrowNotFound()
    {
        // Arrange
        SetupGateway();
        SetupRun(null);
        var query = new GetTestRunQuery
        {
            TestSuiteId = _suiteId,
            RunId = Guid.NewGuid(),
            CurrentUserId = _ownerId,
        };

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Valid_ShouldReturnModel()
    {
        // Arrange
        SetupGateway();
        var run = new TestRun
        {
            Id = Guid.NewGuid(),
            TestSuiteId = _suiteId,
            EnvironmentId = Guid.NewGuid(),
            TriggeredById = _ownerId,
            RunNumber = 7,
            Status = TestRunStatus.Completed,
        };
        SetupRun(run);

        var query = new GetTestRunQuery
        {
            TestSuiteId = _suiteId,
            RunId = run.Id,
            CurrentUserId = _ownerId,
        };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(run.Id);
        result.TestSuiteId.Should().Be(_suiteId);
        result.RunNumber.Should().Be(7);
        result.Status.Should().Be(nameof(TestRunStatus.Completed));
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

    private void SetupRun(TestRun run)
    {
        _runRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(run != null
                ? new List<TestRun> { run }.AsQueryable()
                : new List<TestRun>().AsQueryable());
        _runRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestRun>>()))
            .ReturnsAsync(run);
    }
}
