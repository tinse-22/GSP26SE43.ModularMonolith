using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestReporting.Commands;
using ClassifiedAds.Modules.TestReporting.Controllers;
using ClassifiedAds.Modules.TestReporting.Models;
using ClassifiedAds.Modules.TestReporting.Models.Requests;
using ClassifiedAds.Modules.TestReporting.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestReporting;

public class TestReportsControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ICommandHandler<GenerateTestReportCommand>> _generateHandlerMock;
    private readonly Mock<IQueryHandler<GetTestRunReportsQuery, List<TestReportModel>>> _getReportsHandlerMock;
    private readonly Mock<IQueryHandler<GetTestRunReportQuery, TestReportModel>> _getReportHandlerMock;
    private readonly Mock<IQueryHandler<DownloadTestRunReportQuery, TestReportFileModel>> _downloadHandlerMock;
    private readonly TestReportsController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public TestReportsControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _generateHandlerMock = new Mock<ICommandHandler<GenerateTestReportCommand>>();
        _getReportsHandlerMock = new Mock<IQueryHandler<GetTestRunReportsQuery, List<TestReportModel>>>();
        _getReportHandlerMock = new Mock<IQueryHandler<GetTestRunReportQuery, TestReportModel>>();
        _downloadHandlerMock = new Mock<IQueryHandler<DownloadTestRunReportQuery, TestReportFileModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<GenerateTestReportCommand>)))
            .Returns(_generateHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetTestRunReportsQuery, List<TestReportModel>>)))
            .Returns(_getReportsHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetTestRunReportQuery, TestReportModel>)))
            .Returns(_getReportHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<DownloadTestRunReportQuery, TestReportFileModel>)))
            .Returns(_downloadHandlerMock.Object);

        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new TestReportsController(dispatcher, _currentUserMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GenerateTestRunReport_Should_ReturnCreatedAtActionWithReportPayload()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var expected = CreateReportModel(suiteId, runId, reportId, "Detailed", "JSON");

        _generateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateTestReportCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateTestReportCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.GenerateTestRunReport(suiteId, runId, new GenerateTestReportRequest
        {
            ReportType = "Detailed",
            Format = "JSON",
            RecentHistoryLimit = 7,
        });

        var created = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        created.ActionName.Should().Be(nameof(TestReportsController.GetTestRunReport));
        created.RouteValues!["suiteId"].Should().Be(suiteId);
        created.RouteValues["runId"].Should().Be(runId);
        created.RouteValues["reportId"].Should().Be(reportId);
        created.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GenerateTestRunReport_Should_MapRouteBodyAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        GenerateTestReportCommand capturedCommand = null!;

        _generateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateTestReportCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateTestReportCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateReportModel(suiteId, runId, Guid.NewGuid(), "Coverage", "HTML");
            })
            .Returns(Task.CompletedTask);

        await _controller.GenerateTestRunReport(suiteId, runId, new GenerateTestReportRequest
        {
            ReportType = "Coverage",
            Format = "HTML",
            RecentHistoryLimit = 3,
        });

        capturedCommand.Should().NotBeNull();
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.RunId.Should().Be(runId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.ReportType.Should().Be("Coverage");
        capturedCommand.Format.Should().Be("HTML");
        capturedCommand.RecentHistoryLimit.Should().Be(3);
    }

    [Fact]
    public async Task GenerateTestRunReport_Should_AllowNullRequestAndPreserveNullBodyFields()
    {
        GenerateTestReportCommand capturedCommand = null!;
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        _generateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateTestReportCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateTestReportCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateReportModel(suiteId, runId, Guid.NewGuid(), "Summary", "CSV");
            })
            .Returns(Task.CompletedTask);

        await _controller.GenerateTestRunReport(suiteId, runId, null!);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ReportType.Should().BeNull();
        capturedCommand.Format.Should().BeNull();
        capturedCommand.RecentHistoryLimit.Should().BeNull();
    }

    [Fact]
    public async Task GenerateTestRunReport_Should_PropagateConflictException()
    {
        _generateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateTestReportCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("REPORT_RUN_NOT_READY", "run not finished"));

        var act = () => _controller.GenerateTestRunReport(Guid.NewGuid(), Guid.NewGuid(), new GenerateTestReportRequest
        {
            ReportType = "Detailed",
            Format = "JSON",
        });

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("REPORT_RUN_NOT_READY");
    }

    [Fact]
    public async Task GetTestRunReports_Should_ReturnOkWithReportList()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var expected = new List<TestReportModel>
        {
            CreateReportModel(suiteId, runId, Guid.NewGuid(), "Detailed", "JSON"),
            CreateReportModel(suiteId, runId, Guid.NewGuid(), "Coverage", "HTML"),
        };

        _getReportsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunReportsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.GetTestRunReports(suiteId, runId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<List<TestReportModel>>().Subject.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetTestRunReports_Should_MapRouteIdsAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        GetTestRunReportsQuery capturedQuery = null!;

        _getReportsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunReportsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestRunReportsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<TestReportModel>());

        await _controller.GetTestRunReports(suiteId, runId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.TestSuiteId.Should().Be(suiteId);
        capturedQuery.RunId.Should().Be(runId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetTestRunReports_Should_ReturnEmptyList()
    {
        _getReportsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunReportsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestReportModel>());

        var result = await _controller.GetTestRunReports(Guid.NewGuid(), Guid.NewGuid());

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<List<TestReportModel>>().Subject;
        payload.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTestRunReport_Should_ReturnOkWithSingleReport()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid();

        _getReportHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateReportModel(suiteId, runId, reportId, "Summary", "CSV"));

        var result = await _controller.GetTestRunReport(suiteId, runId, reportId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<TestReportModel>().Subject.Id.Should().Be(reportId);
    }

    [Fact]
    public async Task GetTestRunReport_Should_MapIdentifiersAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        GetTestRunReportQuery capturedQuery = null!;

        _getReportHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestRunReportQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateReportModel(suiteId, runId, reportId, "Detailed", "PDF"));

        await _controller.GetTestRunReport(suiteId, runId, reportId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.TestSuiteId.Should().Be(suiteId);
        capturedQuery.RunId.Should().Be(runId);
        capturedQuery.ReportId.Should().Be(reportId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetTestRunReport_Should_PropagateNotFoundException()
    {
        _getReportHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestRunReportQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("REPORT_NOT_FOUND"));

        var act = () => _controller.GetTestRunReport(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*REPORT_NOT_FOUND*");
    }

    [Fact]
    public async Task DownloadTestRunReport_Should_ReturnFileContentResultWithMetadata()
    {
        var file = new TestReportFileModel
        {
            Content = new byte[] { 1, 2, 3 },
            ContentType = "application/pdf",
            FileName = "report.pdf",
        };

        _downloadHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DownloadTestRunReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(file);

        var result = await _controller.DownloadTestRunReport(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("report.pdf");
        fileResult.FileContents.Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task DownloadTestRunReport_Should_MapIdentifiersAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        DownloadTestRunReportQuery capturedQuery = null!;

        _downloadHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DownloadTestRunReportQuery>(), It.IsAny<CancellationToken>()))
            .Callback<DownloadTestRunReportQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new TestReportFileModel
            {
                Content = new byte[] { 9 },
                FileName = "runtime.json",
                ContentType = "application/json",
            });

        await _controller.DownloadTestRunReport(suiteId, runId, reportId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.TestSuiteId.Should().Be(suiteId);
        capturedQuery.RunId.Should().Be(runId);
        capturedQuery.ReportId.Should().Be(reportId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task DownloadTestRunReport_Should_DefaultContentTypeToOctetStream()
    {
        _downloadHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DownloadTestRunReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestReportFileModel
            {
                Content = new byte[] { 4, 5 },
                FileName = "report.bin",
                ContentType = "",
            });

        var result = await _controller.DownloadTestRunReport(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/octet-stream");
    }

    [Fact]
    public async Task DownloadTestRunReport_Should_HtmlEncodeFileName()
    {
        _downloadHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DownloadTestRunReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestReportFileModel
            {
                Content = new byte[] { 7, 8 },
                FileName = "<report>.html",
                ContentType = "text/html",
            });

        var result = await _controller.DownloadTestRunReport(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileDownloadName.Should().Be("&lt;report&gt;.html");
    }

    [Fact]
    public async Task DownloadTestRunReport_Should_PropagateNotFoundException()
    {
        _downloadHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DownloadTestRunReportQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("REPORT_FILE_NOT_FOUND"));

        var act = () => _controller.DownloadTestRunReport(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*REPORT_FILE_NOT_FOUND*");
    }

    private static TestReportModel CreateReportModel(
        Guid suiteId,
        Guid runId,
        Guid reportId,
        string reportType,
        string format)
    {
        return new TestReportModel
        {
            Id = reportId,
            TestSuiteId = suiteId,
            TestRunId = runId,
            ReportType = reportType,
            Format = format,
            DownloadUrl = $"/api/test-suites/{suiteId}/test-runs/{runId}/reports/{reportId}/download",
            GeneratedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            Coverage = new CoverageMetricModel
            {
                TestRunId = runId,
                TotalEndpoints = 12,
                TestedEndpoints = 10,
                CoveragePercent = 83.33m,
            },
        };
    }
}
