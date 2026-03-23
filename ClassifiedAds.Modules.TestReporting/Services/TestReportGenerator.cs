using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Storage.DTOs;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestReporting.ConfigurationOptions;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Services;

public class TestReportGenerator : ITestReportGenerator
{
    private readonly IRepository<TestReport, Guid> _reportRepository;
    private readonly IRepository<CoverageMetric, Guid> _coverageRepository;
    private readonly IStorageFileGatewayService _storageFileGatewayService;
    private readonly IApiEndpointMetadataService _apiEndpointMetadataService;
    private readonly IReportDataSanitizer _reportDataSanitizer;
    private readonly ICoverageCalculator _coverageCalculator;
    private readonly IReadOnlyCollection<IReportRenderer> _renderers;
    private readonly ReportGenerationOptions _options;

    public TestReportGenerator(
        IRepository<TestReport, Guid> reportRepository,
        IRepository<CoverageMetric, Guid> coverageRepository,
        IStorageFileGatewayService storageFileGatewayService,
        IApiEndpointMetadataService apiEndpointMetadataService,
        IReportDataSanitizer reportDataSanitizer,
        ICoverageCalculator coverageCalculator,
        IEnumerable<IReportRenderer> renderers,
        IOptions<TestReportingModuleOptions> options)
    {
        _reportRepository = reportRepository;
        _coverageRepository = coverageRepository;
        _storageFileGatewayService = storageFileGatewayService;
        _apiEndpointMetadataService = apiEndpointMetadataService;
        _reportDataSanitizer = reportDataSanitizer;
        _coverageCalculator = coverageCalculator;
        _renderers = renderers?.ToArray() ?? Array.Empty<IReportRenderer>();
        _options = options?.Value?.ReportGeneration ?? new ReportGenerationOptions();
    }

    public async Task<TestReportModel> GenerateAsync(
        TestRunReportContextDto context,
        ReportType reportType,
        ReportFormat format,
        Guid generatedById,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Run);

        var sanitizedContext = _reportDataSanitizer.Sanitize(context);
        var metadata = await LoadEndpointMetadataAsync(sanitizedContext, ct);
        var coverage = _coverageCalculator.Calculate(sanitizedContext, metadata);
        var generatedAt = DateTimeOffset.UtcNow;
        var document = BuildDocument(sanitizedContext, reportType, format, coverage, generatedAt);
        var renderer = ResolveRenderer(format);
        var renderedFile = await renderer.RenderAsync(document, ct);

        StorageUploadedFileDTO uploadedFile;
        await using (var contentStream = new MemoryStream(renderedFile.Content, writable: false))
        {
            uploadedFile = await _storageFileGatewayService.UploadAsync(new StorageUploadFileRequest
            {
                FileName = renderedFile.FileName,
                ContentType = renderedFile.ContentType,
                FileSize = renderedFile.Content.LongLength,
                FileCategory = renderedFile.FileCategory,
                OwnerId = generatedById,
                Content = contentStream,
            }, ct);
        }

        var coverageEntity = await _coverageRepository.FirstOrDefaultAsync(
            _coverageRepository.GetQueryableSet().Where(x => x.TestRunId == sanitizedContext.Run.TestRunId));

        var report = new TestReport
        {
            Id = Guid.NewGuid(),
            TestRunId = sanitizedContext.Run.TestRunId,
            GeneratedById = generatedById,
            FileId = uploadedFile.Id,
            ReportType = reportType,
            Format = format,
            GeneratedAt = generatedAt,
            ExpiresAt = ResolveExpiration(generatedAt),
        };

        await _reportRepository.UnitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            if (coverageEntity == null)
            {
                coverageEntity = new CoverageMetric
                {
                    Id = Guid.NewGuid(),
                    TestRunId = sanitizedContext.Run.TestRunId,
                };

                ApplyCoverage(coverageEntity, coverage);
                await _coverageRepository.AddAsync(coverageEntity, transactionCt);
            }
            else
            {
                ApplyCoverage(coverageEntity, coverage);
                await _coverageRepository.UpdateAsync(coverageEntity, transactionCt);
            }

            await _reportRepository.AddAsync(report, transactionCt);
            await _reportRepository.UnitOfWork.SaveChangesAsync(transactionCt);
        }, cancellationToken: ct);

        return TestReportModel.FromEntity(report, sanitizedContext.TestSuiteId, coverage);
    }

    private async Task<IReadOnlyList<ApiEndpointMetadataDto>> LoadEndpointMetadataAsync(TestRunReportContextDto context, CancellationToken ct)
    {
        var orderedEndpointIds = (context.OrderedEndpointIds ?? Array.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();

        if (!context.ApiSpecId.HasValue || orderedEndpointIds.Length == 0)
        {
            return Array.Empty<ApiEndpointMetadataDto>();
        }

        return await _apiEndpointMetadataService.GetEndpointMetadataAsync(context.ApiSpecId.Value, orderedEndpointIds, ct)
            ?? Array.Empty<ApiEndpointMetadataDto>();
    }

    private IReportRenderer ResolveRenderer(ReportFormat format)
    {
        var renderer = _renderers.FirstOrDefault(x => x.Format == format);
        if (renderer == null)
        {
            throw new ValidationException($"REPORT_FORMAT_NOT_SUPPORTED: Format '{format}' is not supported.");
        }

        return renderer;
    }

    private static TestRunReportDocumentModel BuildDocument(
        TestRunReportContextDto context,
        ReportType reportType,
        ReportFormat format,
        CoverageMetricModel coverage,
        DateTimeOffset generatedAt)
    {
        return new TestRunReportDocumentModel
        {
            TestSuiteId = context.TestSuiteId,
            ProjectId = context.ProjectId,
            ApiSpecId = context.ApiSpecId,
            SuiteName = context.SuiteName,
            ReportType = reportType,
            GeneratedAt = generatedAt,
            FileBaseName = BuildFileBaseName(context.Run.RunNumber, reportType, format, generatedAt),
            Run = context.Run,
            Coverage = coverage,
            FailureDistribution = BuildFailureDistribution(context.Results),
            RecentRuns = context.RecentRuns?.ToArray() ?? Array.Empty<TestRunHistoryItemDto>(),
            Cases = BuildCases(context),
        };
    }

    private static IReadOnlyDictionary<string, int> BuildFailureDistribution(IReadOnlyList<ReportTestCaseResultDto> results)
    {
        var distribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var result in results ?? Array.Empty<ReportTestCaseResultDto>())
        {
            if (!string.Equals(result.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (result.FailureReasons == null || result.FailureReasons.Count == 0)
            {
                Increment(distribution, "FAILED");
                continue;
            }

            foreach (var failureReason in result.FailureReasons)
            {
                Increment(distribution, string.IsNullOrWhiteSpace(failureReason.Code) ? "FAILED" : failureReason.Code);
            }
        }

        return distribution
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<TestRunReportCaseDocumentModel> BuildCases(TestRunReportContextDto context)
    {
        var definitions = (context.Definitions ?? Array.Empty<ReportTestCaseDefinitionDto>())
            .GroupBy(x => x.TestCaseId)
            .ToDictionary(x => x.Key, x => x.First());
        var results = (context.Results ?? Array.Empty<ReportTestCaseResultDto>())
            .GroupBy(x => x.TestCaseId)
            .ToDictionary(x => x.Key, x => x.First());

        return definitions.Keys
            .Union(results.Keys)
            .Select(testCaseId =>
            {
                definitions.TryGetValue(testCaseId, out var definition);
                results.TryGetValue(testCaseId, out var result);

                return new TestRunReportCaseDocumentModel
                {
                    TestCaseId = testCaseId,
                    EndpointId = definition?.EndpointId ?? result?.EndpointId,
                    Name = definition?.Name ?? result?.Name,
                    Description = definition?.Description,
                    TestType = definition?.TestType,
                    OrderIndex = definition?.OrderIndex ?? result?.OrderIndex ?? 0,
                    DependencyIds = definition?.DependencyIds?.ToArray() ?? result?.DependencyIds?.ToArray() ?? Array.Empty<Guid>(),
                    Request = definition?.Request,
                    Expectation = definition?.Expectation,
                    Status = result?.Status,
                    HttpStatusCode = result?.HttpStatusCode,
                    DurationMs = result?.DurationMs ?? 0,
                    ResolvedUrl = result?.ResolvedUrl ?? definition?.Request?.Url,
                    RequestHeaders = result?.RequestHeaders != null
                        ? new Dictionary<string, string>(result.RequestHeaders, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    ResponseHeaders = result?.ResponseHeaders != null
                        ? new Dictionary<string, string>(result.ResponseHeaders, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    ResponseBodyPreview = result?.ResponseBodyPreview,
                    FailureReasons = result?.FailureReasons?.ToArray() ?? Array.Empty<ReportValidationFailureDto>(),
                    ExtractedVariables = result?.ExtractedVariables != null
                        ? new Dictionary<string, string>(result.ExtractedVariables, StringComparer.OrdinalIgnoreCase)
                        : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    SkippedBecauseDependencyIds = result?.SkippedBecauseDependencyIds?.ToArray() ?? Array.Empty<Guid>(),
                    StatusCodeMatched = result?.StatusCodeMatched ?? false,
                    SchemaMatched = result?.SchemaMatched,
                    HeaderChecksPassed = result?.HeaderChecksPassed,
                    BodyContainsPassed = result?.BodyContainsPassed,
                    BodyNotContainsPassed = result?.BodyNotContainsPassed,
                    JsonPathChecksPassed = result?.JsonPathChecksPassed,
                    ResponseTimePassed = result?.ResponseTimePassed,
                };
            })
            .OrderBy(x => x.OrderIndex)
            .ThenBy(x => x.TestCaseId)
            .ToArray();
    }

    private static string BuildFileBaseName(int runNumber, ReportType reportType, ReportFormat format, DateTimeOffset generatedAt)
    {
        return $"test-run-{runNumber}-{reportType.ToString().ToLowerInvariant()}-{format.ToString().ToLowerInvariant()}-{generatedAt.UtcDateTime:yyyyMMddTHHmmssZ}";
    }

    private DateTimeOffset? ResolveExpiration(DateTimeOffset generatedAt)
    {
        return _options.ReportRetentionHours > 0
            ? generatedAt.AddHours(_options.ReportRetentionHours)
            : null;
    }

    private static void ApplyCoverage(CoverageMetric entity, CoverageMetricModel model)
    {
        entity.TotalEndpoints = model.TotalEndpoints;
        entity.TestedEndpoints = model.TestedEndpoints;
        entity.CoveragePercent = model.CoveragePercent;
        entity.ByMethod = model.SerializeByMethod();
        entity.ByTag = model.SerializeByTag();
        entity.UncoveredPaths = model.SerializeUncoveredPaths();
        entity.CalculatedAt = model.CalculatedAt;
    }

    private static void Increment(IDictionary<string, int> distribution, string key)
    {
        if (distribution.TryGetValue(key, out var current))
        {
            distribution[key] = current + 1;
            return;
        }

        distribution[key] = 1;
    }
}
