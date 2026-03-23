using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Storage.DTOs;
using ClassifiedAds.Contracts.Storage.Enums;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestReporting.ConfigurationOptions;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using ClassifiedAds.Modules.TestReporting.Services;
using Microsoft.Extensions.Options;
using System.Data;

namespace ClassifiedAds.UnitTests.TestReporting;

public class TestReportGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_ShouldSelectRendererUploadUpsertCoverageAndReturnMetadata()
    {
        // Arrange
        var reportRepositoryMock = new Mock<IRepository<TestReport, Guid>>();
        var coverageRepositoryMock = new Mock<IRepository<CoverageMetric, Guid>>();
        var storageGatewayMock = new Mock<IStorageFileGatewayService>();
        var endpointMetadataServiceMock = new Mock<IApiEndpointMetadataService>();
        var sanitizerMock = new Mock<IReportDataSanitizer>();
        var coverageCalculatorMock = new Mock<ICoverageCalculator>();
        var jsonRendererMock = new Mock<IReportRenderer>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var context = ReportTestData.CreateContext();
        var coverage = ReportTestData.CreateCoverageModel();
        var existingCoverage = new CoverageMetric
        {
            Id = Guid.NewGuid(),
            TestRunId = ReportTestData.RunId,
        };
        TestReport persistedReport = null;

        reportRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(unitOfWorkMock.Object);
        unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(async (operation, _, token) => await operation(token));
        unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        coverageRepositoryMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<CoverageMetric> { existingCoverage }.AsQueryable());
        coverageRepositoryMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<CoverageMetric>>()))
            .ReturnsAsync((IQueryable<CoverageMetric> query) => query.FirstOrDefault());

        sanitizerMock.Setup(x => x.Sanitize(context)).Returns(context);
        endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                ReportTestData.ApiSpecId,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 3 && ids.Contains(ReportTestData.EndpointIdOrders)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReportTestData.CreateMetadata());
        coverageCalculatorMock.Setup(x => x.Calculate(
                context,
                It.IsAny<IReadOnlyCollection<ApiEndpointMetadataDto>>()))
            .Returns(coverage);

        jsonRendererMock.SetupGet(x => x.Format).Returns(ReportFormat.JSON);
        jsonRendererMock.Setup(x => x.RenderAsync(It.IsAny<TestRunReportDocumentModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedReportFile
            {
                Content = System.Text.Encoding.UTF8.GetBytes("{\"ok\":true}"),
                ContentType = "application/json; charset=utf-8",
                FileCategory = FileCategory.Export,
                FileName = "test-run-5-detailed-json.json",
            });

        storageGatewayMock.Setup(x => x.UploadAsync(
                It.Is<StorageUploadFileRequest>(request =>
                    request.FileCategory == FileCategory.Export
                    && request.FileName == "test-run-5-detailed-json.json"
                    && request.OwnerId.HasValue),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUploadedFileDTO
            {
                Id = Guid.Parse("12121212-1212-1212-1212-121212121212"),
            });

        reportRepositoryMock.Setup(x => x.AddAsync(It.IsAny<TestReport>(), It.IsAny<CancellationToken>()))
            .Callback<TestReport, CancellationToken>((report, _) => persistedReport = report)
            .Returns(Task.CompletedTask);
        coverageRepositoryMock.Setup(x => x.UpdateAsync(existingCoverage, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var generator = new TestReportGenerator(
            reportRepositoryMock.Object,
            coverageRepositoryMock.Object,
            storageGatewayMock.Object,
            endpointMetadataServiceMock.Object,
            sanitizerMock.Object,
            coverageCalculatorMock.Object,
            new[] { jsonRendererMock.Object },
            Options.Create(new TestReportingModuleOptions
            {
                ReportGeneration = new ReportGenerationOptions
                {
                    ReportRetentionHours = 24,
                },
            }));

        // Act
        var result = await generator.GenerateAsync(context, ReportType.Detailed, ReportFormat.JSON, Guid.NewGuid());

        // Assert
        result.TestSuiteId.Should().Be(ReportTestData.SuiteId);
        result.TestRunId.Should().Be(ReportTestData.RunId);
        result.ReportType.Should().Be("Detailed");
        result.Format.Should().Be("JSON");
        result.Coverage.Should().BeSameAs(coverage);
        result.DownloadUrl.Should().Contain($"/reports/{result.Id}/download");
        persistedReport.Should().NotBeNull();
        result.Id.Should().Be(persistedReport.Id);
        persistedReport.FileId.Should().Be(Guid.Parse("12121212-1212-1212-1212-121212121212"));
        existingCoverage.TotalEndpoints.Should().Be(3);
        existingCoverage.TestedEndpoints.Should().Be(2);
        existingCoverage.ByMethod.Should().Contain("\"POST\":100");
        existingCoverage.ByTag.Should().Contain("\"payments\":0");
        existingCoverage.UncoveredPaths.Should().Contain("/api/payments");
        jsonRendererMock.Verify(x => x.RenderAsync(It.IsAny<TestRunReportDocumentModel>(), It.IsAny<CancellationToken>()), Times.Once);
        storageGatewayMock.Verify(x => x.UploadAsync(It.IsAny<StorageUploadFileRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        coverageRepositoryMock.Verify(x => x.UpdateAsync(existingCoverage, It.IsAny<CancellationToken>()), Times.Once);
        reportRepositoryMock.Verify(x => x.AddAsync(It.IsAny<TestReport>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWorkMock.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_ShouldSelectRequestedRendererAndAddCoverageWhenMissing()
    {
        // Arrange
        var reportRepositoryMock = new Mock<IRepository<TestReport, Guid>>();
        var coverageRepositoryMock = new Mock<IRepository<CoverageMetric, Guid>>();
        var storageGatewayMock = new Mock<IStorageFileGatewayService>();
        var endpointMetadataServiceMock = new Mock<IApiEndpointMetadataService>();
        var sanitizerMock = new Mock<IReportDataSanitizer>();
        var coverageCalculatorMock = new Mock<ICoverageCalculator>();
        var htmlRendererMock = new Mock<IReportRenderer>();
        var jsonRendererMock = new Mock<IReportRenderer>();
        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var context = ReportTestData.CreateContext();
        var coverage = ReportTestData.CreateCoverageModel();
        CoverageMetric persistedCoverage = null;

        reportRepositoryMock.SetupGet(x => x.UnitOfWork).Returns(unitOfWorkMock.Object);
        unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(async (operation, _, token) => await operation(token));
        unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        coverageRepositoryMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<CoverageMetric>().AsQueryable());
        coverageRepositoryMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<CoverageMetric>>()))
            .ReturnsAsync((CoverageMetric)null);
        coverageRepositoryMock.Setup(x => x.AddAsync(It.IsAny<CoverageMetric>(), It.IsAny<CancellationToken>()))
            .Callback<CoverageMetric, CancellationToken>((entity, _) => persistedCoverage = entity)
            .Returns(Task.CompletedTask);

        sanitizerMock.Setup(x => x.Sanitize(context)).Returns(context);
        endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                ReportTestData.ApiSpecId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReportTestData.CreateMetadata());
        coverageCalculatorMock.Setup(x => x.Calculate(
                context,
                It.IsAny<IReadOnlyCollection<ApiEndpointMetadataDto>>()))
            .Returns(coverage);

        htmlRendererMock.SetupGet(x => x.Format).Returns(ReportFormat.HTML);
        htmlRendererMock.Setup(x => x.RenderAsync(It.IsAny<TestRunReportDocumentModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedReportFile
            {
                Content = System.Text.Encoding.UTF8.GetBytes("<html></html>"),
                ContentType = "text/html; charset=utf-8",
                FileCategory = FileCategory.Report,
                FileName = "test-run-5-summary-html.html",
            });

        jsonRendererMock.SetupGet(x => x.Format).Returns(ReportFormat.JSON);

        storageGatewayMock.Setup(x => x.UploadAsync(
                It.Is<StorageUploadFileRequest>(request =>
                    request.FileCategory == FileCategory.Report
                    && request.FileName == "test-run-5-summary-html.html"
                    && request.OwnerId == ReportTestData.CreateContext().CreatedById),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageUploadedFileDTO
            {
                Id = Guid.Parse("34343434-3434-3434-3434-343434343434"),
            });

        var generator = new TestReportGenerator(
            reportRepositoryMock.Object,
            coverageRepositoryMock.Object,
            storageGatewayMock.Object,
            endpointMetadataServiceMock.Object,
            sanitizerMock.Object,
            coverageCalculatorMock.Object,
            new[] { jsonRendererMock.Object, htmlRendererMock.Object },
            Options.Create(new TestReportingModuleOptions()));

        // Act
        var result = await generator.GenerateAsync(
            context,
            ReportType.Summary,
            ReportFormat.HTML,
            context.CreatedById);

        // Assert
        result.Format.Should().Be("HTML");
        htmlRendererMock.Verify(x => x.RenderAsync(It.IsAny<TestRunReportDocumentModel>(), It.IsAny<CancellationToken>()), Times.Once);
        jsonRendererMock.Verify(x => x.RenderAsync(It.IsAny<TestRunReportDocumentModel>(), It.IsAny<CancellationToken>()), Times.Never);
        coverageRepositoryMock.Verify(x => x.AddAsync(It.IsAny<CoverageMetric>(), It.IsAny<CancellationToken>()), Times.Once);
        coverageRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<CoverageMetric>(), It.IsAny<CancellationToken>()), Times.Never);
        persistedCoverage.Should().NotBeNull();
        persistedCoverage.TestRunId.Should().Be(ReportTestData.RunId);
        persistedCoverage.TotalEndpoints.Should().Be(coverage.TotalEndpoints);
        persistedCoverage.TestedEndpoints.Should().Be(coverage.TestedEndpoints);
        storageGatewayMock.Verify(x => x.UploadAsync(It.IsAny<StorageUploadFileRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GenerateAsync_WhenUploadFails_ShouldNotPersistMetadata()
    {
        // Arrange
        var reportRepositoryMock = new Mock<IRepository<TestReport, Guid>>();
        var coverageRepositoryMock = new Mock<IRepository<CoverageMetric, Guid>>();
        var storageGatewayMock = new Mock<IStorageFileGatewayService>();
        var endpointMetadataServiceMock = new Mock<IApiEndpointMetadataService>();
        var sanitizerMock = new Mock<IReportDataSanitizer>();
        var coverageCalculatorMock = new Mock<ICoverageCalculator>();
        var rendererMock = new Mock<IReportRenderer>();
        var context = ReportTestData.CreateContext();

        sanitizerMock.Setup(x => x.Sanitize(context)).Returns(context);
        endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReportTestData.CreateMetadata());
        coverageCalculatorMock.Setup(x => x.Calculate(context, It.IsAny<IReadOnlyCollection<ApiEndpointMetadataDto>>()))
            .Returns(ReportTestData.CreateCoverageModel());
        rendererMock.SetupGet(x => x.Format).Returns(ReportFormat.JSON);
        rendererMock.Setup(x => x.RenderAsync(It.IsAny<TestRunReportDocumentModel>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenderedReportFile
            {
                Content = System.Text.Encoding.UTF8.GetBytes("{}"),
                ContentType = "application/json; charset=utf-8",
                FileCategory = FileCategory.Export,
                FileName = "report.json",
            });
        storageGatewayMock.Setup(x => x.UploadAsync(It.IsAny<StorageUploadFileRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("upload failed"));

        var generator = new TestReportGenerator(
            reportRepositoryMock.Object,
            coverageRepositoryMock.Object,
            storageGatewayMock.Object,
            endpointMetadataServiceMock.Object,
            sanitizerMock.Object,
            coverageCalculatorMock.Object,
            new[] { rendererMock.Object },
            Options.Create(new TestReportingModuleOptions()));

        // Act
        var act = () => generator.GenerateAsync(context, ReportType.Detailed, ReportFormat.JSON, Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        coverageRepositoryMock.Verify(x => x.AddAsync(It.IsAny<CoverageMetric>(), It.IsAny<CancellationToken>()), Times.Never);
        coverageRepositoryMock.Verify(x => x.UpdateAsync(It.IsAny<CoverageMetric>(), It.IsAny<CancellationToken>()), Times.Never);
        reportRepositoryMock.Verify(x => x.AddAsync(It.IsAny<TestReport>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
