using ClassifiedAds.Application;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Queries;

public class GetTestRunReportsQuery : IQuery<List<TestReportModel>>
{
    public Guid TestSuiteId { get; set; }

    public Guid RunId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetTestRunReportsQueryHandler : IQueryHandler<GetTestRunReportsQuery, List<TestReportModel>>
{
    private readonly IRepository<TestReport, Guid> _reportRepository;
    private readonly IRepository<CoverageMetric, Guid> _coverageRepository;
    private readonly ITestExecutionReadGatewayService _gatewayService;

    public GetTestRunReportsQueryHandler(
        IRepository<TestReport, Guid> reportRepository,
        IRepository<CoverageMetric, Guid> coverageRepository,
        ITestExecutionReadGatewayService gatewayService)
    {
        _reportRepository = reportRepository;
        _coverageRepository = coverageRepository;
        _gatewayService = gatewayService;
    }

    public async Task<List<TestReportModel>> HandleAsync(GetTestRunReportsQuery query, CancellationToken cancellationToken = default)
    {
        if (query.TestSuiteId == Guid.Empty)
        {
            throw new ValidationException("TestSuiteId là bắt buộc.");
        }

        if (query.RunId == Guid.Empty)
        {
            throw new ValidationException("RunId là bắt buộc.");
        }

        var suiteContext = await _gatewayService.GetSuiteAccessContextAsync(query.TestSuiteId, cancellationToken);
        if (suiteContext.CreatedById != query.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        var coverage = await GetCoverageAsync(query.RunId);
        var reports = await _reportRepository.ToListAsync(
            _reportRepository.GetQueryableSet()
                .Where(x => x.TestRunId == query.RunId)
                .OrderByDescending(x => x.GeneratedAt)
                .ThenByDescending(x => x.Id));

        return reports
            .Select(x => TestReportModel.FromEntity(x, query.TestSuiteId, coverage))
            .ToList();
    }

    private async Task<CoverageMetricModel> GetCoverageAsync(Guid runId)
    {
        var coverage = await _coverageRepository.FirstOrDefaultAsync(
            _coverageRepository.GetQueryableSet()
                .Where(x => x.TestRunId == runId));

        return CoverageMetricModel.FromEntity(coverage);
    }
}
