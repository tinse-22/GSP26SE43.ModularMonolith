using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Queries;
using System.Threading;

namespace ClassifiedAds.UnitTests.TestReporting;

public class GetTestRunReportQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenReportIsMissing_ShouldThrowNotFoundException()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var reportId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var reportRepositoryMock = new Mock<IRepository<TestReport, Guid>>();
        var coverageRepositoryMock = new Mock<IRepository<CoverageMetric, Guid>>();
        var gatewayMock = new Mock<ITestExecutionReadGatewayService>();

        reportRepositoryMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestReport>().AsQueryable());
        reportRepositoryMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestReport>>()))
            .ReturnsAsync((TestReport)null);
        gatewayMock.Setup(x => x.GetSuiteAccessContextAsync(suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteAccessContextDto
            {
                TestSuiteId = suiteId,
                CreatedById = ownerId,
            });

        var handler = new GetTestRunReportQueryHandler(
            reportRepositoryMock.Object,
            coverageRepositoryMock.Object,
            gatewayMock.Object);

        // Act
        var act = () => handler.HandleAsync(new GetTestRunReportQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            ReportId = reportId,
            CurrentUserId = ownerId,
        });

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
