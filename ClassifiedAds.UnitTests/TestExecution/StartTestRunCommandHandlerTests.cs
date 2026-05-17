using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Commands;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class StartTestRunCommandHandlerTests
{
    private readonly Mock<IRepository<TestRun, Guid>> _runRepoMock;
    private readonly Mock<IRepository<ExecutionEnvironment, Guid>> _envRepoMock;
    private readonly Mock<ITestExecutionReadGatewayService> _gatewayMock;
    private readonly Mock<ISubscriptionLimitGatewayService> _limitServiceMock;
    private readonly Mock<ITestExecutionOrchestrator> _orchestratorMock;
    private readonly StartTestRunCommandHandler _handler;

    private readonly Guid _suiteId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _envId = Guid.NewGuid();

    public StartTestRunCommandHandlerTests()
    {
        _runRepoMock = new Mock<IRepository<TestRun, Guid>>();
        _envRepoMock = new Mock<IRepository<ExecutionEnvironment, Guid>>();
        _gatewayMock = new Mock<ITestExecutionReadGatewayService>();
        _limitServiceMock = new Mock<ISubscriptionLimitGatewayService>();
        _orchestratorMock = new Mock<ITestExecutionOrchestrator>();

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        unitOfWorkMock
            .Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (action, _, ct) => await action(ct));
        unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _runRepoMock.Setup(x => x.UnitOfWork).Returns(unitOfWorkMock.Object);
        _runRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestRun>().AsQueryable());
        _envRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<ExecutionEnvironment>().AsQueryable());

        _handler = new StartTestRunCommandHandler(
            _runRepoMock.Object,
            _envRepoMock.Object,
            _gatewayMock.Object,
            _limitServiceMock.Object,
            _orchestratorMock.Object,
            new Mock<ILogger<StartTestRunCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_EmptyTestSuiteId_ShouldThrowValidation()
    {
        // Arrange
        var command = new StartTestRunCommand
        {
            TestSuiteId = Guid.Empty,
            CurrentUserId = _userId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_WrongOwner_ShouldThrowValidation()
    {
        // Arrange
        SetupSuiteAccess(createdById: Guid.NewGuid()); // different owner

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId, // not the owner
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Message.Contains("quyền"));
    }

    [Fact]
    public async Task HandleAsync_SuiteNotReady_ShouldThrowValidation()
    {
        // Arrange
        SetupSuiteAccess(status: "Draft");

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Message.Contains("sẵn sàng"));
    }

    [Fact]
    public async Task HandleAsync_SelectedTestCaseNotInSuite_ShouldThrowValidation()
    {
        // Arrange
        SetupSuiteAccess();

        var selectedId = Guid.NewGuid();
        _gatewayMock
            .Setup(x => x.GetTestCaseIdsBySuiteAsync(_suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { Guid.NewGuid() });

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId,
            SelectedTestCaseIds = new List<Guid> { selectedId },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Message.Contains("không thuộc suite hoặc đã bị vô hiệu hóa"));
    }

    [Fact]
    public async Task HandleAsync_SelectedTestCaseInSuite_ShouldCallOrchestratorWithNormalizedIds()
    {
        // Arrange
        SetupFullHappyPath();

        var selectedId = Guid.NewGuid();
        _gatewayMock
            .Setup(x => x.GetTestCaseIdsBySuiteAsync(_suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { selectedId });

        IReadOnlyCollection<Guid> capturedSelectedIds = Array.Empty<Guid>();
        _orchestratorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<TestRunRetryPolicyModel>(),
                It.IsAny<ValidationProfile>()))
            .Callback<Guid, Guid, IReadOnlyCollection<Guid>, CancellationToken, bool, TestRunRetryPolicyModel, ValidationProfile>((_, _, ids, _, _, _, _) =>
                capturedSelectedIds = ids)
            .ReturnsAsync(new TestRunResultModel
            {
                Run = new TestRunModel(),
                Cases = new List<TestCaseRunResultModel>(),
            });

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId,
            EnvironmentId = _envId,
            SelectedTestCaseIds = new List<Guid> { selectedId, Guid.Empty, selectedId },
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        capturedSelectedIds.Should().BeEquivalentTo(new[] { selectedId });
    }

    [Fact]
    public async Task HandleAsync_StrictValidationEnabled_ShouldPassFlagToOrchestrator()
    {
        // Arrange
        SetupSuiteAccess();
        SetupEnvironment();
        SetupLimits();
        SetupRunAllocation();

        var selectedId = Guid.NewGuid();
        _gatewayMock
            .Setup(x => x.GetTestCaseIdsBySuiteAsync(_suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid> { selectedId });

        var capturedStrictValidation = false;
        _orchestratorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<TestRunRetryPolicyModel>(),
                It.IsAny<ValidationProfile>()))
            .Callback<Guid, Guid, IReadOnlyCollection<Guid>, CancellationToken, bool, TestRunRetryPolicyModel, ValidationProfile>((_, _, _, _, strict, _, _) =>
                capturedStrictValidation = strict)
            .ReturnsAsync(new TestRunResultModel
            {
                Run = new TestRunModel(),
                Cases = new List<TestCaseRunResultModel>(),
            });

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId,
            EnvironmentId = _envId,
            SelectedTestCaseIds = new List<Guid> { selectedId },
            StrictValidation = true,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        capturedStrictValidation.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_WithEnvironmentId_ShouldResolveSpecificEnvironment()
    {
        // Arrange
        SetupFullHappyPath();

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId,
            EnvironmentId = _envId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.Should().NotBeNull();
        _orchestratorMock.Verify(
            x => x.ExecuteAsync(
                It.IsAny<Guid>(),
                _userId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<TestRunRetryPolicyModel>(),
                It.IsAny<ValidationProfile>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WithoutEnvironmentId_ShouldFallbackToDefault()
    {
        // Arrange
        var defaultEnv = new ExecutionEnvironment
        {
            Id = _envId,
            ProjectId = _projectId,
            Name = "Default Env",
            BaseUrl = "https://api.example.com",
            IsDefault = true,
        };

        SetupSuiteAccess();
        _envRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ExecutionEnvironment>>()))
            .ReturnsAsync(defaultEnv);
        SetupLimits();
        SetupRunAllocation();
        SetupOrchestrator();

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId,
            EnvironmentId = null, // no specific env
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_NoDefaultEnvironmentFound_ShouldThrowNotFound()
    {
        // Arrange
        SetupSuiteAccess();
        _envRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ExecutionEnvironment>>()))
            .ReturnsAsync((ExecutionEnvironment)null);

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId,
            EnvironmentId = null,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>()
            .Where(ex => ex.Message.Contains("mặc định"));
    }

    [Fact]
    public async Task HandleAsync_ConcurrentRunLimitExceeded_ShouldThrowValidation()
    {
        // Arrange
        SetupSuiteAccess();
        SetupEnvironment();

        // Running runs exist
        _runRepoMock.Setup(x => x.CountAsync(It.IsAny<IQueryable<TestRun>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        _limitServiceMock
            .Setup(x => x.CheckLimitAsync(_userId, LimitType.MaxConcurrentRuns, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = false, DenialReason = "Limit exceeded" });

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId,
            EnvironmentId = _envId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Message.Contains("đồng thời"));
    }

    [Fact]
    public async Task HandleAsync_MonthlyQuotaExceeded_ShouldThrowValidation()
    {
        // Arrange
        SetupSuiteAccess();
        SetupEnvironment();

        _runRepoMock.Setup(x => x.CountAsync(It.IsAny<IQueryable<TestRun>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _limitServiceMock
            .Setup(x => x.CheckLimitAsync(It.IsAny<Guid>(), LimitType.MaxConcurrentRuns, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });

        _limitServiceMock
            .Setup(x => x.TryConsumeLimitAsync(_userId, LimitType.MaxTestRunsPerMonth, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = false, DenialReason = "Monthly limit exceeded" });

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId,
            EnvironmentId = _envId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Message.Contains("tháng"));
    }

    [Fact]
    public async Task HandleAsync_Success_ShouldAllocateRunNumberAndCallOrchestrator()
    {
        // Arrange
        SetupFullHappyPath();

        TestRun capturedRun = null;
        _runRepoMock.Setup(x => x.AddAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .Callback<TestRun, CancellationToken>((run, _) => capturedRun = run)
            .Returns(Task.CompletedTask);

        var command = new StartTestRunCommand
        {
            TestSuiteId = _suiteId,
            CurrentUserId = _userId,
            EnvironmentId = _envId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        capturedRun.Should().NotBeNull();
        capturedRun.RunNumber.Should().Be(1);
        capturedRun.Status.Should().Be(TestRunStatus.Pending);
        capturedRun.RedisKey.Should().StartWith("testrun:");
        command.Result.Should().NotBeNull();
    }

    #region Helpers

    private void SetupSuiteAccess(Guid? createdById = null, string status = "Ready")
    {
        _gatewayMock
            .Setup(x => x.GetSuiteAccessContextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteAccessContextDto
            {
                TestSuiteId = _suiteId,
                ProjectId = _projectId,
                CreatedById = createdById ?? _userId,
                Status = status,
            });
    }

    private void SetupEnvironment()
    {
        var env = new ExecutionEnvironment
        {
            Id = _envId,
            ProjectId = _projectId,
            Name = "Test Env",
            BaseUrl = "https://api.example.com",
            IsDefault = false,
        };
        _envRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ExecutionEnvironment>>()))
            .ReturnsAsync(env);
    }

    private void SetupLimits()
    {
        _runRepoMock.Setup(x => x.CountAsync(It.IsAny<IQueryable<TestRun>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _limitServiceMock
            .Setup(x => x.CheckLimitAsync(It.IsAny<Guid>(), LimitType.MaxConcurrentRuns, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });

        _limitServiceMock
            .Setup(x => x.TryConsumeLimitAsync(It.IsAny<Guid>(), LimitType.MaxTestRunsPerMonth, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });
    }

    private void SetupRunAllocation()
    {
        _runRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<int?>>()))
            .ReturnsAsync((int?)null);
        _runRepoMock.Setup(x => x.AddAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupOrchestrator()
    {
        _orchestratorMock
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<bool>(),
                It.IsAny<TestRunRetryPolicyModel>(),
                It.IsAny<ValidationProfile>()))
            .ReturnsAsync(new TestRunResultModel
            {
                Run = new TestRunModel(),
                Cases = new List<TestCaseRunResultModel>(),
            });
    }

    private void SetupFullHappyPath()
    {
        SetupSuiteAccess();
        SetupEnvironment();
        SetupLimits();
        SetupRunAllocation();
        SetupOrchestrator();
    }

    #endregion
}
