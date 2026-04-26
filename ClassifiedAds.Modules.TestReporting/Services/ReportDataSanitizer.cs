using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestReporting.ConfigurationOptions;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestReporting.Services;

public class ReportDataSanitizer : IReportDataSanitizer
{
    private const string MaskedValue = "***MASKED***";

    private static readonly string[] SensitiveKeywords =
    {
        "authorization",
        "cookie",
        "set-cookie",
        "token",
        "secret",
        "password",
        "apikey",
        "api-key",
    };

    private static readonly Regex BearerTokenRegex = new Regex(
        @"(?i)\bbearer\s+[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.Compiled);

    private static readonly Regex JsonSecretRegex = new Regex(
        "(?i)(?<prefix>\"(?:[^\"]*(?:authorization|cookie|set-cookie|token|secret|password|api(?:-|)?key)[^\"]*)\"\\s*:\\s*\")(?<value>[^\"]+)(?<suffix>\")",
        RegexOptions.Compiled);

    private static readonly Regex PairSecretRegex = new Regex(
        @"(?i)(?<prefix>\b(?:[\w$.\-\[\]]*(?:authorization|cookie|set-cookie|token|secret|password|api(?:-|)?key)[\w$.\-\[\]]*)\b\s*[:=]\s*)(?<value>[^\s,;""'\]\}&]+)",
        RegexOptions.Compiled);

    private readonly int _maxResponseBodyPreviewChars;

    public ReportDataSanitizer(IOptions<TestReportingModuleOptions> options)
    {
        var configuredPreviewLength = options?.Value?.ReportGeneration?.MaxResponseBodyPreviewChars
            ?? new ReportGenerationOptions().MaxResponseBodyPreviewChars;

        _maxResponseBodyPreviewChars = Math.Max(0, configuredPreviewLength);
    }

    public TestRunReportContextDto Sanitize(TestRunReportContextDto context)
    {
        if (context == null)
        {
            return null;
        }

        return new TestRunReportContextDto
        {
            TestSuiteId = context.TestSuiteId,
            ProjectId = context.ProjectId,
            ProjectName = SanitizeText(context.ProjectName),
            ApiSpecId = context.ApiSpecId,
            CreatedById = context.CreatedById,
            SuiteName = SanitizeText(context.SuiteName),
            Run = SanitizeRun(context.Run),
            RecentRuns = context.RecentRuns?.Select(CloneRecentRun).ToArray() ?? Array.Empty<TestRunHistoryItemDto>(),
            OrderedEndpointIds = context.OrderedEndpointIds?.ToArray() ?? Array.Empty<Guid>(),
            Definitions = context.Definitions?.Select(SanitizeDefinition).ToArray() ?? Array.Empty<ReportTestCaseDefinitionDto>(),
            Results = context.Results?.Select(SanitizeResult).ToArray() ?? Array.Empty<ReportTestCaseResultDto>(),
            Attempts = context.Attempts?.Select(SanitizeAttempt).ToArray() ?? Array.Empty<TestRunExecutionAttemptDto>(),
        };
    }

    private static TestRunReportRunDto SanitizeRun(TestRunReportRunDto run)
    {
        if (run == null)
        {
            return null;
        }

        return new TestRunReportRunDto
        {
            TestRunId = run.TestRunId,
            RunNumber = run.RunNumber,
            EnvironmentId = run.EnvironmentId,
            TriggeredById = run.TriggeredById,
            Status = run.Status,
            StartedAt = run.StartedAt,
            CompletedAt = run.CompletedAt,
            DurationMs = run.DurationMs,
            TotalTests = run.TotalTests,
            PassedCount = run.PassedCount,
            FailedCount = run.FailedCount,
            SkippedCount = run.SkippedCount,
            ResolvedEnvironmentName = SanitizeText(run.ResolvedEnvironmentName),
            ExecutedAt = run.ExecutedAt,
        };
    }

    private static TestRunHistoryItemDto CloneRecentRun(TestRunHistoryItemDto item)
    {
        if (item == null)
        {
            return null;
        }

        return new TestRunHistoryItemDto
        {
            TestRunId = item.TestRunId,
            RunNumber = item.RunNumber,
            Status = item.Status,
            CompletedAt = item.CompletedAt,
            DurationMs = item.DurationMs,
            PassedCount = item.PassedCount,
            FailedCount = item.FailedCount,
            SkippedCount = item.SkippedCount,
        };
    }

    private ReportTestCaseDefinitionDto SanitizeDefinition(ReportTestCaseDefinitionDto definition)
    {
        if (definition == null)
        {
            return null;
        }

        return new ReportTestCaseDefinitionDto
        {
            TestCaseId = definition.TestCaseId,
            EndpointId = definition.EndpointId,
            Name = SanitizeText(definition.Name),
            Description = SanitizeText(definition.Description),
            TestType = definition.TestType,
            OrderIndex = definition.OrderIndex,
            DependencyIds = definition.DependencyIds?.ToArray() ?? Array.Empty<Guid>(),
            Request = SanitizeRequest(definition.Request),
            Expectation = SanitizeExpectation(definition.Expectation),
        };
    }

    private static ExecutionTestCaseRequestDto SanitizeRequest(ExecutionTestCaseRequestDto request)
    {
        if (request == null)
        {
            return null;
        }

        return new ExecutionTestCaseRequestDto
        {
            HttpMethod = request.HttpMethod,
            Url = SanitizeText(request.Url),
            Headers = SanitizeText(request.Headers),
            PathParams = SanitizeText(request.PathParams),
            QueryParams = SanitizeText(request.QueryParams),
            BodyType = request.BodyType,
            Body = SanitizeText(request.Body),
            Timeout = request.Timeout,
        };
    }

    private static ExecutionTestCaseExpectationDto SanitizeExpectation(ExecutionTestCaseExpectationDto expectation)
    {
        if (expectation == null)
        {
            return null;
        }

        return new ExecutionTestCaseExpectationDto
        {
            ExpectedStatus = expectation.ExpectedStatus,
            ResponseSchema = SanitizeText(expectation.ResponseSchema),
            HeaderChecks = SanitizeText(expectation.HeaderChecks),
            BodyContains = SanitizeText(expectation.BodyContains),
            BodyNotContains = SanitizeText(expectation.BodyNotContains),
            JsonPathChecks = SanitizeText(expectation.JsonPathChecks),
            MaxResponseTime = expectation.MaxResponseTime,
        };
    }

    private ReportTestCaseResultDto SanitizeResult(ReportTestCaseResultDto result)
    {
        if (result == null)
        {
            return null;
        }

        return new ReportTestCaseResultDto
        {
            TestCaseId = result.TestCaseId,
            EndpointId = result.EndpointId,
            Name = SanitizeText(result.Name),
            OrderIndex = result.OrderIndex,
            Status = result.Status,
            HttpStatusCode = result.HttpStatusCode,
            DurationMs = result.DurationMs,
            ResolvedUrl = SanitizeText(result.ResolvedUrl),
            RequestHeaders = SanitizeDictionary(result.RequestHeaders),
            ResponseHeaders = SanitizeDictionary(result.ResponseHeaders),
            ResponseBodyPreview = TruncatePreview(SanitizeText(result.ResponseBodyPreview)),
            FailureReasons = result.FailureReasons?.Select(x => new ReportValidationFailureDto
            {
                Code = x.Code,
                Message = SanitizeText(x.Message),
                Target = x.Target,
                Expected = SanitizeText(x.Expected),
                Actual = SanitizeText(x.Actual),
            }).ToArray() ?? Array.Empty<ReportValidationFailureDto>(),
            ExtractedVariables = SanitizeDictionary(result.ExtractedVariables),
            DependencyIds = result.DependencyIds?.ToArray() ?? Array.Empty<Guid>(),
            SkippedBecauseDependencyIds = result.SkippedBecauseDependencyIds?.ToArray() ?? Array.Empty<Guid>(),
            StatusCodeMatched = result.StatusCodeMatched,
            SchemaMatched = result.SchemaMatched,
            HeaderChecksPassed = result.HeaderChecksPassed,
            BodyContainsPassed = result.BodyContainsPassed,
            BodyNotContainsPassed = result.BodyNotContainsPassed,
            JsonPathChecksPassed = result.JsonPathChecksPassed,
            ResponseTimePassed = result.ResponseTimePassed,
        };
    }

    private static TestRunExecutionAttemptDto SanitizeAttempt(TestRunExecutionAttemptDto attempt)
    {
        if (attempt == null)
        {
            return null;
        }

        return new TestRunExecutionAttemptDto
        {
            ExecutionAttemptId = attempt.ExecutionAttemptId,
            TestCaseId = attempt.TestCaseId,
            ParentAttemptId = attempt.ParentAttemptId,
            AttemptNumber = attempt.AttemptNumber,
            Status = attempt.Status,
            RetryReason = SanitizeText(attempt.RetryReason),
            SkippedCause = SanitizeText(attempt.SkippedCause),
            DurationMs = attempt.DurationMs,
            StartedAt = attempt.StartedAt,
            CompletedAt = attempt.CompletedAt,
            FailureReasons = attempt.FailureReasons?.Select(x => new ReportValidationFailureDto
            {
                Code = x.Code,
                Message = SanitizeText(x.Message),
                Target = x.Target,
                Expected = SanitizeText(x.Expected),
                Actual = SanitizeText(x.Actual),
            }).ToArray() ?? Array.Empty<ReportValidationFailureDto>(),
        };
    }

    private static Dictionary<string, string> SanitizeDictionary(IReadOnlyDictionary<string, string> source)
    {
        if (source == null || source.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return source
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => IsSensitiveKey(x.Key) ? MaskedValue : SanitizeText(x.Value),
                StringComparer.OrdinalIgnoreCase);
    }

    private string TruncatePreview(string value)
    {
        if (string.IsNullOrEmpty(value) || _maxResponseBodyPreviewChars <= 0)
        {
            return value;
        }

        return value.Length > _maxResponseBodyPreviewChars
            ? value[.._maxResponseBodyPreviewChars]
            : value;
    }

    private static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return SensitiveKeywords.Any(keyword => key.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string SanitizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var sanitized = BearerTokenRegex.Replace(value, "Bearer ***MASKED***");
        sanitized = JsonSecretRegex.Replace(
            sanitized,
            match => $"{match.Groups["prefix"].Value}{MaskedValue}{match.Groups["suffix"].Value}");
        sanitized = PairSecretRegex.Replace(
            sanitized,
            match => $"{match.Groups["prefix"].Value}{MaskedValue}");

        return sanitized;
    }
}
