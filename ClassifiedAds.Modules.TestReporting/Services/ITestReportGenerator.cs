using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Services;

public interface ITestReportGenerator
{
    Task<TestReportModel> GenerateAsync(
        TestRunReportContextDto context,
        ReportType reportType,
        ReportFormat format,
        Guid generatedById,
        CancellationToken ct = default);
}
