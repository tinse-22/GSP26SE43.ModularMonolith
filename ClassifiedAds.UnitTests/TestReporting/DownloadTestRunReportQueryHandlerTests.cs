using ClassifiedAds.Contracts.Storage.DTOs;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Queries;
using System.Threading;

namespace ClassifiedAds.UnitTests.TestReporting;

public class DownloadTestRunReportQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenStorageFileIsMissing_ShouldThrowNotFoundException()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var reportRepositoryMock = new Mock<IRepository<TestReport, Guid>>();
        var gatewayMock = new Mock<ITestExecutionReadGatewayService>();
        var storageGatewayMock = new Mock<IStorageFileGatewayService>();
        var report = new TestReport
        {
            Id = reportId,
            TestRunId = runId,
            GeneratedById = ownerId,
            FileId = fileId,
            ReportType = ReportType.Detailed,
            Format = ReportFormat.HTML,
            GeneratedAt = DateTimeOffset.UtcNow,
        };

        reportRepositoryMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestReport> { report }.AsQueryable());
        reportRepositoryMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestReport>>()))
            .ReturnsAsync((IQueryable<TestReport> query) => query.FirstOrDefault());
        gatewayMock.Setup(x => x.GetSuiteAccessContextAsync(suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteAccessContextDto
            {
                TestSuiteId = suiteId,
                CreatedById = ownerId,
            });
        storageGatewayMock.Setup(x => x.DownloadAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageDownloadResult
            {
                Content = Array.Empty<byte>(),
                ContentType = "text/plain",
                FileName = "report.txt",
            });

        var handler = new DownloadTestRunReportQueryHandler(
            reportRepositoryMock.Object,
            gatewayMock.Object,
            storageGatewayMock.Object);

        // Act
        var act = () => handler.HandleAsync(new DownloadTestRunReportQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            ReportId = reportId,
            CurrentUserId = ownerId,
        });

        // Assert
        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.Message.Should().Contain("REPORT_FILE_NOT_FOUND");
        storageGatewayMock.Verify(x => x.DownloadAsync(fileId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ShouldDownloadUsingResolvedFileId()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var reportRepositoryMock = new Mock<IRepository<TestReport, Guid>>();
        var gatewayMock = new Mock<ITestExecutionReadGatewayService>();
        var storageGatewayMock = new Mock<IStorageFileGatewayService>();
        var report = new TestReport
        {
            Id = reportId,
            TestRunId = runId,
            GeneratedById = ownerId,
            FileId = fileId,
            ReportType = ReportType.Detailed,
            Format = ReportFormat.HTML,
            GeneratedAt = DateTimeOffset.UtcNow,
        };

        reportRepositoryMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestReport> { report }.AsQueryable());
        reportRepositoryMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestReport>>()))
            .ReturnsAsync((IQueryable<TestReport> query) => query.FirstOrDefault());
        gatewayMock.Setup(x => x.GetSuiteAccessContextAsync(suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteAccessContextDto
            {
                TestSuiteId = suiteId,
                CreatedById = ownerId,
            });
        storageGatewayMock.Setup(x => x.DownloadAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageDownloadResult
            {
                Content = "hello"u8.ToArray(),
                ContentType = "text/plain",
                FileName = "report.txt",
            });

        var handler = new DownloadTestRunReportQueryHandler(
            reportRepositoryMock.Object,
            gatewayMock.Object,
            storageGatewayMock.Object);

        // Act
        var result = await handler.HandleAsync(new DownloadTestRunReportQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            ReportId = reportId,
            CurrentUserId = ownerId,
        });

        // Assert
        result.Should().NotBeNull();
        result.FileName.Should().Be("report.txt");
        result.ContentType.Should().Be("text/plain");
        storageGatewayMock.Verify(x => x.DownloadAsync(fileId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
