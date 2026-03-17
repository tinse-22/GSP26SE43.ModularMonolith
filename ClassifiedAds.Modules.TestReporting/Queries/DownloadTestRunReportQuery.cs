using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Queries;

public class DownloadTestRunReportQuery : IQuery<TestReportFileModel>
{
    public Guid TestSuiteId { get; set; }

    public Guid RunId { get; set; }

    public Guid ReportId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class DownloadTestRunReportQueryHandler : IQueryHandler<DownloadTestRunReportQuery, TestReportFileModel>
{
    private readonly IRepository<TestReport, Guid> _reportRepository;
    private readonly ITestExecutionReadGatewayService _gatewayService;
    private readonly IStorageFileGatewayService _storageFileGatewayService;

    public DownloadTestRunReportQueryHandler(
        IRepository<TestReport, Guid> reportRepository,
        ITestExecutionReadGatewayService gatewayService,
        IStorageFileGatewayService storageFileGatewayService)
    {
        _reportRepository = reportRepository;
        _gatewayService = gatewayService;
        _storageFileGatewayService = storageFileGatewayService;
    }

    public async Task<TestReportFileModel> HandleAsync(DownloadTestRunReportQuery query, CancellationToken cancellationToken = default)
    {
        if (query.TestSuiteId == Guid.Empty)
        {
            throw new ValidationException("TestSuiteId la bat buoc.");
        }

        if (query.RunId == Guid.Empty)
        {
            throw new ValidationException("RunId la bat buoc.");
        }

        if (query.ReportId == Guid.Empty)
        {
            throw new ValidationException("ReportId la bat buoc.");
        }

        var suiteContext = await _gatewayService.GetSuiteAccessContextAsync(query.TestSuiteId, cancellationToken);
        if (suiteContext.CreatedById != query.CurrentUserId)
        {
            throw new ValidationException("Ban khong co quyen thao tac test suite nay.");
        }

        var report = await _reportRepository.FirstOrDefaultAsync(
            _reportRepository.GetQueryableSet()
                .Where(x => x.Id == query.ReportId && x.TestRunId == query.RunId));

        if (report == null)
        {
            throw new NotFoundException($"REPORT_NOT_FOUND: Khong tim thay report metadata voi ma '{query.ReportId}'.");
        }

        try
        {
            var file = await _storageFileGatewayService.DownloadAsync(report.FileId, cancellationToken);
            if (file == null || file.Content == null || file.Content.Length == 0)
            {
                throw new NotFoundException("REPORT_FILE_NOT_FOUND: Khong tim thay file report trong storage.");
            }

            return TestReportFileModel.FromStorageDownload(file);
        }
        catch (NotFoundException)
        {
            throw new NotFoundException("REPORT_FILE_NOT_FOUND: Khong tim thay file report trong storage.");
        }
    }
}
