using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestExecution.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestReporting.Commands;
using ClassifiedAds.Modules.TestReporting.ConfigurationOptions;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using ClassifiedAds.Modules.TestReporting.Services;
using Microsoft.Extensions.Options;
using System.Threading;

namespace ClassifiedAds.UnitTests.TestReporting;

public class GenerateTestReportCommandHandlerTests
{
    private readonly Mock<ITestRunReportReadGatewayService> _reportGatewayMock;
    private readonly Mock<ITestReportGenerator> _reportGeneratorMock;
    private readonly Guid _suiteId = Guid.NewGuid();
    private readonly Guid _runId = Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();

    public GenerateTestReportCommandHandlerTests()
    {
        _reportGatewayMock = new Mock<ITestRunReportReadGatewayService>();
        _reportGeneratorMock = new Mock<ITestReportGenerator>();
    }

    [Fact]
    public async Task HandleAsync_WhenCurrentUserIsNotOwner_ShouldThrowValidationException()
    {
        // Arrange
        var handler = CreateHandler();
        _reportGatewayMock
            .Setup(x => x.GetReportContextAsync(_suiteId, _runId, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRunReportContextDto
            {
                TestSuiteId = _suiteId,
                CreatedById = Guid.NewGuid(),
                Run = new TestRunReportRunDto { TestRunId = _runId },
            });

        var command = new GenerateTestReportCommand
        {
            TestSuiteId = _suiteId,
            RunId = _runId,
            CurrentUserId = _ownerId,
            ReportType = "Detailed",
            Format = "Json",
        };

        // Act
        var act = () => handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _reportGeneratorMock.Verify(
            x => x.GenerateAsync(
                It.IsAny<TestRunReportContextDto>(),
                It.IsAny<ReportType>(),
                It.IsAny<ReportFormat>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenRunIsNotReady_ShouldPropagateConflictAndNotInvokeGenerator()
    {
        // Arrange
        var handler = CreateHandler();
        _reportGatewayMock
            .Setup(x => x.GetReportContextAsync(_suiteId, _runId, 5, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("REPORT_RUN_NOT_READY", "run not finished"));

        var command = new GenerateTestReportCommand
        {
            TestSuiteId = _suiteId,
            RunId = _runId,
            CurrentUserId = _ownerId,
            ReportType = "Detailed",
            Format = "Json",
        };

        // Act
        var act = () => handler.HandleAsync(command);

        // Assert
        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("REPORT_RUN_NOT_READY");
        _reportGeneratorMock.Verify(
            x => x.GenerateAsync(
                It.IsAny<TestRunReportContextDto>(),
                It.IsAny<ReportType>(),
                It.IsAny<ReportFormat>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenCommandIsValid_ShouldDelegateToGeneratorWithNormalizedValues()
    {
        // Arrange
        var handler = CreateHandler(defaultHistoryLimit: 6);
        var context = new TestRunReportContextDto
        {
            TestSuiteId = _suiteId,
            CreatedById = _ownerId,
            Run = new TestRunReportRunDto { TestRunId = _runId },
        };
        var expected = new TestReportModel
        {
            Id = Guid.NewGuid(),
            TestSuiteId = _suiteId,
            TestRunId = _runId,
            ReportType = "Detailed",
            Format = "JSON",
        };

        _reportGatewayMock
            .Setup(x => x.GetReportContextAsync(_suiteId, _runId, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        _reportGeneratorMock
            .Setup(x => x.GenerateAsync(context, ReportType.Detailed, ReportFormat.JSON, _ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var command = new GenerateTestReportCommand
        {
            TestSuiteId = _suiteId,
            RunId = _runId,
            CurrentUserId = _ownerId,
            ReportType = " detailed ",
            Format = " json ",
        };

        // Act
        await handler.HandleAsync(command);

        // Assert
        command.Result.Should().BeSameAs(expected);
        _reportGatewayMock.Verify(x => x.GetReportContextAsync(_suiteId, _runId, 6, It.IsAny<CancellationToken>()), Times.Once);
        _reportGeneratorMock.Verify(
            x => x.GenerateAsync(context, ReportType.Detailed, ReportFormat.JSON, _ownerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private GenerateTestReportCommandHandler CreateHandler(int defaultHistoryLimit = 5, int maxHistoryLimit = 20)
    {
        return new GenerateTestReportCommandHandler(
            _reportGatewayMock.Object,
            _reportGeneratorMock.Object,
            Options.Create(new TestReportingModuleOptions
            {
                ReportGeneration = new ReportGenerationOptions
                {
                    DefaultHistoryLimit = defaultHistoryLimit,
                    MaxHistoryLimit = maxHistoryLimit,
                },
            }));
    }
}
