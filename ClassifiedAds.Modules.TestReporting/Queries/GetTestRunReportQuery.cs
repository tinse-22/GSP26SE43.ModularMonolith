using ClassifiedAds.Application;
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

public class GetTestRunReportQuery : IQuery<TestReportModel>
{
    public Guid TestSuiteId { get; set; }

    public Guid RunId { get; set; }

    public Guid ReportId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetTestRunReportQueryHandler : IQueryHandler<GetTestRunReportQuery, TestReportModel>
{
    private readonly IRepository<TestReport, Guid> _reportRepository;
    private readonly IRepository<CoverageMetric, Guid> _coverageRepository;
    private readonly ITestExecutionReadGatewayService _gatewayService;

    public GetTestRunReportQueryHandler(
        IRepository<TestReport, Guid> reportRepository,
        IRepository<CoverageMetric, Guid> coverageRepository,
        ITestExecutionReadGatewayService gatewayService)
    {
        _reportRepository = reportRepository;
        _coverageRepository = coverageRepository;
        _gatewayService = gatewayService;
    }

    public async Task<TestReportModel> HandleAsync(GetTestRunReportQuery query, CancellationToken cancellationToken = default)
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

        var coverage = await _coverageRepository.FirstOrDefaultAsync(
            _coverageRepository.GetQueryableSet()
                .Where(x => x.TestRunId == query.RunId));

        return TestReportModel.FromEntity(report, query.TestSuiteId, CoverageMetricModel.FromEntity(coverage));
    }
}
