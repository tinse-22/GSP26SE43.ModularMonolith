using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Queries;
using System.Threading;

namespace ClassifiedAds.UnitTests.TestReporting;

public class GetTestRunReportsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenCurrentUserIsNotOwner_ShouldThrowValidationException()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var reportRepositoryMock = new Mock<IRepository<TestReport, Guid>>();
        var coverageRepositoryMock = new Mock<IRepository<CoverageMetric, Guid>>();
        var gatewayMock = new Mock<ITestExecutionReadGatewayService>();

        gatewayMock.Setup(x => x.GetSuiteAccessContextAsync(suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteAccessContextDto
            {
                TestSuiteId = suiteId,
                CreatedById = ownerId,
            });

        var handler = new GetTestRunReportsQueryHandler(
            reportRepositoryMock.Object,
            coverageRepositoryMock.Object,
            gatewayMock.Object);

        // Act
        var act = () => handler.HandleAsync(new GetTestRunReportsQuery
        {
            TestSuiteId = suiteId,
            RunId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
        });

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        reportRepositoryMock.Verify(x => x.ToListAsync(It.IsAny<IQueryable<TestReport>>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_ShouldReturnOnlyReportsForRequestedRun()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var requestedRunId = Guid.NewGuid();
        var anotherRunId = Guid.NewGuid();
        var requestedReport = new TestReport
        {
            Id = Guid.NewGuid(),
            TestRunId = requestedRunId,
            GeneratedById = ownerId,
            FileId = Guid.NewGuid(),
            ReportType = ReportType.Summary,
            Format = ReportFormat.JSON,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
        var anotherReport = new TestReport
        {
            Id = Guid.NewGuid(),
            TestRunId = anotherRunId,
            GeneratedById = ownerId,
            FileId = Guid.NewGuid(),
            ReportType = ReportType.Coverage,
            Format = ReportFormat.PDF,
            GeneratedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        };
        var reports = new List<TestReport> { requestedReport, anotherReport };
        var reportRepositoryMock = new Mock<IRepository<TestReport, Guid>>();
        var coverageRepositoryMock = new Mock<IRepository<CoverageMetric, Guid>>();
        var gatewayMock = new Mock<ITestExecutionReadGatewayService>();

        reportRepositoryMock.Setup(x => x.GetQueryableSet()).Returns(reports.AsQueryable());
        reportRepositoryMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestReport>>()))
            .ReturnsAsync((IQueryable<TestReport> query) => query.ToList());
        coverageRepositoryMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<CoverageMetric>
            {
                new CoverageMetric
                {
                    Id = Guid.NewGuid(),
                    TestRunId = requestedRunId,
                    TotalEndpoints = 5,
                    TestedEndpoints = 4,
                    CoveragePercent = 80,
                },
            }.AsQueryable());
        coverageRepositoryMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<CoverageMetric>>()))
            .ReturnsAsync((IQueryable<CoverageMetric> query) => query.FirstOrDefault());
        gatewayMock.Setup(x => x.GetSuiteAccessContextAsync(suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteAccessContextDto
            {
                TestSuiteId = suiteId,
                CreatedById = ownerId,
            });

        var handler = new GetTestRunReportsQueryHandler(
            reportRepositoryMock.Object,
            coverageRepositoryMock.Object,
            gatewayMock.Object);

        // Act
        var result = await handler.HandleAsync(new GetTestRunReportsQuery
        {
            TestSuiteId = suiteId,
            RunId = requestedRunId,
            CurrentUserId = ownerId,
        });

        // Assert
        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(requestedReport.Id);
        result.Single().TestRunId.Should().Be(requestedRunId);
        result.Single().Coverage.Should().NotBeNull();
        result.Single().Coverage.CoveragePercent.Should().Be(80);
    }
}
