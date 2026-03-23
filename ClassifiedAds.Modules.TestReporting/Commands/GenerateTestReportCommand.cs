using ClassifiedAds.Application;
using ClassifiedAds.Contracts.TestExecution.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestReporting.ConfigurationOptions;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using ClassifiedAds.Modules.TestReporting.Services;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Commands;

public class GenerateTestReportCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid RunId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string ReportType { get; set; }

    public string Format { get; set; }

    public int? RecentHistoryLimit { get; set; }

    public TestReportModel Result { get; set; }
}

public class GenerateTestReportCommandHandler : ICommandHandler<GenerateTestReportCommand>
{
    private readonly ITestRunReportReadGatewayService _reportReadGatewayService;
    private readonly ITestReportGenerator _reportGenerator;
    private readonly ReportGenerationOptions _options;

    public GenerateTestReportCommandHandler(
        ITestRunReportReadGatewayService reportReadGatewayService,
        ITestReportGenerator reportGenerator,
        IOptions<TestReportingModuleOptions> options)
    {
        _reportReadGatewayService = reportReadGatewayService;
        _reportGenerator = reportGenerator;
        _options = options?.Value?.ReportGeneration ?? new ReportGenerationOptions();
    }

    public async Task HandleAsync(GenerateTestReportCommand command, CancellationToken cancellationToken = default)
    {
        if (command.TestSuiteId == Guid.Empty)
        {
            throw new ValidationException("TestSuiteId là bắt buộc.");
        }

        if (command.RunId == Guid.Empty)
        {
            throw new ValidationException("RunId là bắt buộc.");
        }

        var recentHistoryLimit = NormalizeRecentHistoryLimit(command.RecentHistoryLimit);
        var context = await _reportReadGatewayService.GetReportContextAsync(
            command.TestSuiteId,
            command.RunId,
            recentHistoryLimit,
            cancellationToken);

        if (context.CreatedById != command.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        var reportType = NormalizeReportType(command.ReportType);
        var format = NormalizeFormat(command.Format);

        command.Result = await _reportGenerator.GenerateAsync(
            context,
            reportType,
            format,
            command.CurrentUserId,
            cancellationToken);
    }

    private int NormalizeRecentHistoryLimit(int? recentHistoryLimit)
    {
        var maxHistoryLimit = ResolveMaxHistoryLimit();

        if (!recentHistoryLimit.HasValue)
        {
            var defaultHistoryLimit = _options.DefaultHistoryLimit > 0
                ? _options.DefaultHistoryLimit
                : new ReportGenerationOptions().DefaultHistoryLimit;

            return Math.Clamp(defaultHistoryLimit, 1, maxHistoryLimit);
        }

        if (recentHistoryLimit.Value < 1)
        {
            throw new ValidationException("RecentHistoryLimit phải lớn hơn hoặc bằng 1.");
        }

        if (recentHistoryLimit.Value > maxHistoryLimit)
        {
            throw new ValidationException($"RecentHistoryLimit không được vượt quá {maxHistoryLimit}.");
        }

        return recentHistoryLimit.Value;
    }

    private int ResolveMaxHistoryLimit()
    {
        return _options.MaxHistoryLimit > 0
            ? _options.MaxHistoryLimit
            : new ReportGenerationOptions().MaxHistoryLimit;
    }

    private static ReportType NormalizeReportType(string reportType)
    {
        if (string.IsNullOrWhiteSpace(reportType))
        {
            throw new ValidationException("ReportType là bắt buộc.");
        }

        var normalized = reportType.Trim();
        if (!Enum.TryParse<ReportType>(normalized, true, out var parsed)
            || !Enum.IsDefined(typeof(ReportType), parsed))
        {
            throw new ValidationException($"ReportType '{reportType}' không hợp lệ.");
        }

        return parsed;
    }

    private static ReportFormat NormalizeFormat(string format)
    {
        if (string.IsNullOrWhiteSpace(format))
        {
            throw new ValidationException("Format là bắt buộc.");
        }

        var normalized = format.Trim();
        if (!Enum.TryParse<ReportFormat>(normalized, true, out var parsed)
            || !Enum.IsDefined(typeof(ReportFormat), parsed))
        {
            throw new ValidationException($"Format '{format}' không được hỗ trợ.");
        }

        return parsed;
    }
}
