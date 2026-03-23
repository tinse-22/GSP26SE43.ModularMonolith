using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public class FailureExplanationSanitizer : IFailureExplanationSanitizer
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

    private static readonly Regex BearerTokenRegex = new (
        @"(?i)\bbearer\s+[A-Za-z0-9\-._~+/]+=*",
        RegexOptions.Compiled);

    private static readonly Regex JsonSecretRegex = new (
        "(?i)(?<prefix>\"(?:[^\"]*(?:authorization|cookie|set-cookie|token|secret|password|api(?:-|)?key)[^\"]*)\"\\s*:\\s*\")(?<value>[^\"]+)(?<suffix>\")",
        RegexOptions.Compiled);

    private static readonly Regex PairSecretRegex = new (
        @"(?i)(?<prefix>\b(?:[\w$.\-\[\]]*(?:authorization|cookie|set-cookie|token|secret|password|api(?:-|)?key)[\w$.\-\[\]]*)\b\s*[:=]\s*)(?<value>[^\s,;""'\]\}&]+)",
        RegexOptions.Compiled);

    public TestFailureExplanationContextDto Sanitize(TestFailureExplanationContextDto context)
    {
        if (context == null)
        {
            return null;
        }

        return new TestFailureExplanationContextDto
        {
            TestSuiteId = context.TestSuiteId,
            ProjectId = context.ProjectId,
            ApiSpecId = context.ApiSpecId,
            CreatedById = context.CreatedById,
            TestRunId = context.TestRunId,
            RunNumber = context.RunNumber,
            TriggeredById = context.TriggeredById,
            ResolvedEnvironmentName = SanitizeText(context.ResolvedEnvironmentName),
            ExecutedAt = context.ExecutedAt,
            Definition = SanitizeDefinition(context.Definition),
            ActualResult = SanitizeActualResult(context.ActualResult),
        };
    }

    private static FailureExplanationDefinitionDto SanitizeDefinition(FailureExplanationDefinitionDto definition)
    {
        if (definition == null)
        {
            return null;
        }

        return new FailureExplanationDefinitionDto
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

    private static FailureExplanationActualResultDto SanitizeActualResult(FailureExplanationActualResultDto actualResult)
    {
        if (actualResult == null)
        {
            return null;
        }

        return new FailureExplanationActualResultDto
        {
            Status = actualResult.Status,
            HttpStatusCode = actualResult.HttpStatusCode,
            DurationMs = actualResult.DurationMs,
            ResolvedUrl = SanitizeText(actualResult.ResolvedUrl),
            RequestHeaders = SanitizeDictionary(actualResult.RequestHeaders),
            ResponseHeaders = SanitizeDictionary(actualResult.ResponseHeaders),
            ResponseBodyPreview = SanitizeText(actualResult.ResponseBodyPreview),
            FailureReasons = actualResult.FailureReasons?
                .Select(x => new FailureExplanationFailureReasonDto
                {
                    Code = x.Code,
                    Message = SanitizeText(x.Message),
                    Target = x.Target,
                    Expected = SanitizeText(x.Expected),
                    Actual = SanitizeText(x.Actual),
                })
                .ToArray() ?? Array.Empty<FailureExplanationFailureReasonDto>(),
            ExtractedVariables = SanitizeDictionary(actualResult.ExtractedVariables),
            DependencyIds = actualResult.DependencyIds?.ToArray() ?? Array.Empty<Guid>(),
            SkippedBecauseDependencyIds = actualResult.SkippedBecauseDependencyIds?.ToArray() ?? Array.Empty<Guid>(),
            StatusCodeMatched = actualResult.StatusCodeMatched,
            SchemaMatched = actualResult.SchemaMatched,
            HeaderChecksPassed = actualResult.HeaderChecksPassed,
            BodyContainsPassed = actualResult.BodyContainsPassed,
            BodyNotContainsPassed = actualResult.BodyNotContainsPassed,
            JsonPathChecksPassed = actualResult.JsonPathChecksPassed,
            ResponseTimePassed = actualResult.ResponseTimePassed,
        };
    }

    private static Dictionary<string, string> SanitizeDictionary(IReadOnlyDictionary<string, string> source)
    {
        if (source == null || source.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        return source.ToDictionary(
            x => x.Key,
            x => IsSensitiveKey(x.Key) ? MaskedValue : SanitizeText(x.Value),
            StringComparer.OrdinalIgnoreCase);
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
