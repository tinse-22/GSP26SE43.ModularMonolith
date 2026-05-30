using ClassifiedAds.Application;
using ClassifiedAds.Application.Common.DTOs;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestExecution.Commands;
using ClassifiedAds.Modules.TestExecution.Controllers;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Models.Requests;
using ClassifiedAds.Modules.TestExecution.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class TestRunsControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<TestRunsController>> _loggerMock;
    private readonly Mock<ICommandHandler<StartTestRunCommand>> _startHandlerMock;
    private readonly Mock<IQueryHandler<GetTestRunsQuery, Paged<TestRunModel>>> _getRunsHandlerMock;
    private readonly Mock<IQueryHandler<GetTestRunQuery, TestRunModel>> _getRunHandlerMock;
    private readonly Mock<IQueryHandler<GetTestRunResultsQuery, TestRunResultModel>> _getResultsHandlerMock;
    private readonly TestRunsController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public TestRunsControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<TestRunsController>>();
        _startHandlerMock = new Mock<ICommandHandler<StartTestRunCommand>>();
        _getRunsHandlerMock = new Mock<IQueryHandler<GetTestRunsQuery, Paged<TestRunModel>>>();
        _getRunHandlerMock = new Mock<IQueryHandler<GetTestRunQuery, TestRunModel>>();
        _getResultsHandlerMock = new Mock<IQueryHandler<GetTestRunResultsQuery, TestRunResultModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<StartTestRunCommand>)))
            .Returns(_startHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetTestRunsQuery, Paged<TestRunModel>>)))
            .Returns(_getRunsHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetTestRunQuery, TestRunModel>)))
            .Returns(_getRunHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetTestRunResultsQuery, TestRunResultModel>)))
            .Returns(_getResultsHandlerMock.Object);

        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new TestRunsController(dispatcher, _currentUserMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task StartTestRun_Should_ReturnCreatedWithResultPayload()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var expected = CreateResultModel(suiteId, runId, caseCount: 2);

        _startHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<StartTestRunCommand>(), It.IsAny<CancellationToken>()))
            .Callback<StartTestRunCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.StartTestRun(suiteId, new StartTestRunRequest
        {
            EnvironmentId = Guid.NewGuid(),
            StrictValidation = true,
            RecordRun = true,
        });

        var createdResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task StartTestRun_Should_MapRouteBodyAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        var selectedA = Guid.NewGuid();
        var selectedB = Guid.NewGuid();
        StartTestRunCommand capturedCommand = null!;
        var retryPolicy = new TestRunRetryPolicyModel
        {
            MaxRetryAttempts = 2,
            EnableRetry = false,
            RerunSkippedCases = false,
        };

        _startHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<StartTestRunCommand>(), It.IsAny<CancellationToken>()))
            .Callback<StartTestRunCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateResultModel(suiteId, Guid.NewGuid());
            })
            .Returns(Task.CompletedTask);

        await _controller.StartTestRun(suiteId, new StartTestRunRequest
        {
            EnvironmentId = environmentId,
            StrictValidation = true,
            MaxRetryAttempts = 1,
            RerunSkippedCases = false,
            EnableRetry = false,
            RetryPolicy = retryPolicy,
            RecordRun = false,
            SelectedTestCaseIds = new List<Guid> { selectedA, Guid.Empty, selectedA, selectedB },
        });

        capturedCommand.Should().NotBeNull();
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.EnvironmentId.Should().Be(environmentId);
        capturedCommand.StrictValidation.Should().BeTrue();
        capturedCommand.MaxRetryAttempts.Should().Be(1);
        capturedCommand.EnableRetry.Should().BeFalse();
        capturedCommand.RerunSkippedCases.Should().BeFalse();
        capturedCommand.RetryPolicy.Should().BeSameAs(retryPolicy);
        capturedCommand.RecordRun.Should().BeFalse();
        capturedCommand.SelectedTestCaseIds.Should().Equal(selectedA, selectedB);
    }

    [Fact]
    public async Task StartTestRun_Should_UseDefaults_WhenRequestIsNull()
    {
        var suiteId = Guid.NewGuid();
        StartTestRunCommand capturedCommand = null!;

        _startHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<StartTestRunCommand>(), It.IsAny<CancellationToken>()))
            .Callback<StartTestRunCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateResultModel(suiteId, Guid.NewGuid());
            })
            .Returns(Task.CompletedTask);

        await _controller.StartTestRun(suiteId, null!);

        capturedCommand.Should().NotBeNull();
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.EnvironmentId.Should().BeNull();
        capturedCommand.StrictValidation.Should().BeFalse();
        capturedCommand.MaxRetryAttempts.Should().Be(0);
        capturedCommand.EnableRetry.Should().BeTrue();
        capturedCommand.RerunSkippedCases.Should().BeTrue();
        capturedCommand.RecordRun.Should().BeTrue();
        capturedCommand.SelectedTestCaseIds.Should().BeNull();
    }

    [Fact]
    public async Task StartTestRun_Should_PropagateValidationException()
    {
        _startHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<StartTestRunCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Invalid suite state"));

        var act = () => _controller.StartTestRun(Guid.NewGuid(), new StartTestRunRequest());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Invalid suite state*");
    }

    [Fact]
    public async Task StartTestRun_Should_PropagateNotFoundException_WhenEnvironmentMissing()
    {
        _startHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<StartTestRunCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Execution environment not found"));

        var act = () => _controller.StartTestRun(Guid.NewGuid(), new StartTestRunRequest
        {
            EnvironmentId = Guid.NewGuid(),
        });

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Execution environment not found*");
    }

    [Fact]
    public async Task StartTestRun_Should_PropagateValidationException_WhenSelectedCasesInvalid()
    {
        _startHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<StartTestRunCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Selected test cases do not belong to this suite"));

        var act = () => _controller.StartTestRun(Guid.NewGuid(), new StartTestRunRequest
        {
            SelectedTestCaseIds = new List<Guid> { Guid.NewGuid() },
        });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Selected test cases do not belong to this suite*");
    }

    [Fact]
    public async Task GetTestRuns_Should_ReturnOkWithPagedRuns()
    {
        var suiteId = Guid.NewGuid();
        var expected = new Paged<TestRunModel>
        {
            Items = new List<TestRunModel>
            {
                CreateRunModel(suiteId, Guid.NewGuid(), 1, "Completed"),
                CreateRunModel(suiteId, Guid.NewGuid(), 2, "Failed"),
            },
            TotalItems = 2,
            Page = 1,
            PageSize = 20,
        };

        _getRunsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.GetTestRuns(suiteId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<Paged<TestRunModel>>().Subject;
        payload.TotalItems.Should().Be(2);
        payload.Items.Should().HaveCount(2);
        payload.Items[0].RunNumber.Should().Be(1);
    }

    [Fact]
    public async Task GetTestRuns_Should_MapPagingStatusAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        GetTestRunsQuery capturedQuery = null!;

        _getRunsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestRunsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new Paged<TestRunModel> { Items = new List<TestRunModel>() });

        await _controller.GetTestRuns(suiteId, pageNumber: 3, pageSize: 15, status: "running", includeEphemeral: true);

        capturedQuery.Should().NotBeNull();
        capturedQuery.TestSuiteId.Should().Be(suiteId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
        capturedQuery.PageNumber.Should().Be(3);
        capturedQuery.PageSize.Should().Be(15);
        capturedQuery.Status.Should().Be(ClassifiedAds.Modules.TestExecution.Entities.TestRunStatus.Running);
        capturedQuery.IncludeEphemeral.Should().BeTrue();
    }

    [Fact]
    public async Task GetTestRuns_Should_DefaultStatusToNull_WhenStatusIsInvalid()
    {
        GetTestRunsQuery capturedQuery = null!;

        _getRunsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestRunsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new Paged<TestRunModel> { Items = new List<TestRunModel>() });

        await _controller.GetTestRuns(Guid.NewGuid(), status: "not-a-real-status");

        capturedQuery.Should().NotBeNull();
        capturedQuery.Status.Should().BeNull();
        capturedQuery.PageNumber.Should().Be(1);
        capturedQuery.PageSize.Should().Be(20);
        capturedQuery.IncludeEphemeral.Should().BeFalse();
    }

    [Fact]
    public async Task GetTestRuns_Should_ReturnEmptyPage()
    {
        _getRunsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Paged<TestRunModel>
            {
                Items = new List<TestRunModel>(),
                TotalItems = 0,
                Page = 1,
                PageSize = 20,
            });

        var result = await _controller.GetTestRuns(Guid.NewGuid(), status: "completed");

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<Paged<TestRunModel>>().Subject;
        payload.Items.Should().BeEmpty();
        payload.TotalItems.Should().Be(0);
    }

    [Fact]
    public async Task GetTestRun_Should_ReturnOkWithRun()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        _getRunHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateRunModel(suiteId, runId, 5, "Running"));

        var result = await _controller.GetTestRun(suiteId, runId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<TestRunModel>().Subject.Id.Should().Be(runId);
    }

    [Fact]
    public async Task GetTestRun_Should_MapRouteIdsAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        GetTestRunQuery capturedQuery = null!;

        _getRunHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestRunQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateRunModel(suiteId, runId, 7, "Completed"));

        await _controller.GetTestRun(suiteId, runId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.TestSuiteId.Should().Be(suiteId);
        capturedQuery.RunId.Should().Be(runId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetTestRun_Should_PropagateNotFoundException()
    {
        _getRunHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Run not found"));

        var act = () => _controller.GetTestRun(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Run not found*");
    }

    [Fact]
    public async Task GetTestRunResults_Should_ReturnOkWithResultPayload()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var expected = CreateResultModel(suiteId, runId, caseCount: 3);

        _getResultsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunResultsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.GetTestRunResults(suiteId, runId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<TestRunResultModel>().Subject;
        payload.Cases.Should().HaveCount(3);
        payload.Run.Should().NotBeNull();
        payload.ResultsSource.Should().Be("cache");
    }

    [Fact]
    public async Task GetTestRunResults_Should_MapRouteIdsAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        GetTestRunResultsQuery capturedQuery = null!;

        _getResultsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunResultsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestRunResultsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateResultModel(suiteId, runId));

        await _controller.GetTestRunResults(suiteId, runId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.TestSuiteId.Should().Be(suiteId);
        capturedQuery.RunId.Should().Be(runId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetTestRunResults_Should_PropagateConflictException()
    {
        _getResultsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunResultsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("Run results are no longer available"));

        var act = () => _controller.GetTestRunResults(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Run results are no longer available*");
    }

    [Fact]
    public async Task GetTestRunResults_Should_PropagateNotFoundException()
    {
        _getResultsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunResultsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Run results not found"));

        var act = () => _controller.GetTestRunResults(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Run results not found*");
    }

    private static TestRunModel CreateRunModel(Guid suiteId, Guid runId, int runNumber, string status)
    {
        return new TestRunModel
        {
            Id = runId,
            TestSuiteId = suiteId,
            EnvironmentId = Guid.NewGuid(),
            RunNumber = runNumber,
            Status = status,
            TotalTests = 5,
            PassedCount = status == "Completed" ? 5 : 2,
            FailedCount = status == "Failed" ? 3 : 0,
            SkippedCount = 0,
            DurationMs = 1234,
            TestSuiteName = "Checkout suite",
            EnvironmentName = "QA",
            CreatedDateTime = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
    }

    private static TestRunResultModel CreateResultModel(Guid suiteId, Guid runId, int caseCount = 1)
    {
        var result = new TestRunResultModel
        {
            Run = CreateRunModel(suiteId, runId, 4, "Completed"),
            ResultsSource = "cache",
            ExecutedAt = DateTimeOffset.UtcNow,
            ResolvedEnvironmentName = "QA",
        };

        for (var i = 0; i < caseCount; i++)
        {
            result.Cases.Add(new TestCaseRunResultModel
            {
                TestCaseId = Guid.NewGuid(),
                Name = $"Case {i + 1}",
                Status = i == caseCount - 1 ? "Failed" : "Passed",
                HttpStatusCode = i == caseCount - 1 ? 500 : 200,
                DurationMs = 100 + i,
            });
        }

        return result;
    }
}
