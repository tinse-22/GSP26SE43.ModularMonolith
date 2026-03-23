using ClassifiedAds.Contracts.TestExecution.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public class FailureExplanationFingerprintBuilder : IFailureExplanationFingerprintBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public string Build(TestFailureExplanationContextDto context)
    {
        if (context == null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var canonicalPayload = new
        {
            resolvedEnvironmentName = context.ResolvedEnvironmentName,
            definition = new
            {
                endpointId = context.Definition?.EndpointId,
                name = context.Definition?.Name,
                description = context.Definition?.Description,
                testType = context.Definition?.TestType,
                orderIndex = context.Definition?.OrderIndex,
                dependencyIds = NormalizeGuids(context.Definition?.DependencyIds),
                request = new
                {
                    httpMethod = context.Definition?.Request?.HttpMethod,
                    url = context.Definition?.Request?.Url,
                    headers = context.Definition?.Request?.Headers,
                    pathParams = context.Definition?.Request?.PathParams,
                    queryParams = context.Definition?.Request?.QueryParams,
                    bodyType = context.Definition?.Request?.BodyType,
                    body = context.Definition?.Request?.Body,
                    timeout = context.Definition?.Request?.Timeout,
                },
                expectation = new
                {
                    expectedStatus = context.Definition?.Expectation?.ExpectedStatus,
                    responseSchema = context.Definition?.Expectation?.ResponseSchema,
                    headerChecks = context.Definition?.Expectation?.HeaderChecks,
                    bodyContains = context.Definition?.Expectation?.BodyContains,
                    bodyNotContains = context.Definition?.Expectation?.BodyNotContains,
                    jsonPathChecks = context.Definition?.Expectation?.JsonPathChecks,
                    maxResponseTime = context.Definition?.Expectation?.MaxResponseTime,
                },
            },
            actualResult = new
            {
                status = context.ActualResult?.Status,
                httpStatusCode = context.ActualResult?.HttpStatusCode,
                durationMs = context.ActualResult?.DurationMs,
                resolvedUrl = context.ActualResult?.ResolvedUrl,
                requestHeaders = NormalizeDictionary(context.ActualResult?.RequestHeaders),
                responseHeaders = NormalizeDictionary(context.ActualResult?.ResponseHeaders),
                responseBodyPreview = context.ActualResult?.ResponseBodyPreview,
                failureReasons = NormalizeFailureReasons(context.ActualResult?.FailureReasons),
                extractedVariables = NormalizeDictionary(context.ActualResult?.ExtractedVariables),
                dependencyIds = NormalizeGuids(context.ActualResult?.DependencyIds),
                skippedBecauseDependencyIds = NormalizeGuids(context.ActualResult?.SkippedBecauseDependencyIds),
                statusCodeMatched = context.ActualResult?.StatusCodeMatched,
                schemaMatched = context.ActualResult?.SchemaMatched,
                headerChecksPassed = context.ActualResult?.HeaderChecksPassed,
                bodyContainsPassed = context.ActualResult?.BodyContainsPassed,
                bodyNotContainsPassed = context.ActualResult?.BodyNotContainsPassed,
                jsonPathChecksPassed = context.ActualResult?.JsonPathChecksPassed,
                responseTimePassed = context.ActualResult?.ResponseTimePassed,
            },
        };

        var json = JsonSerializer.Serialize(canonicalPayload, JsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static IReadOnlyDictionary<string, string> NormalizeDictionary(IReadOnlyDictionary<string, string> source)
    {
        return source == null
            ? new SortedDictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new SortedDictionary<string, string>(
                source.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<Guid> NormalizeGuids(IReadOnlyList<Guid> source)
    {
        return source?.Where(x => x != Guid.Empty).OrderBy(x => x).ToArray() ?? Array.Empty<Guid>();
    }

    private static IReadOnlyList<object> NormalizeFailureReasons(IReadOnlyList<FailureExplanationFailureReasonDto> source)
    {
        return source?
            .Select(x => new
            {
                code = x.Code,
                message = x.Message,
                target = x.Target,
                expected = x.Expected,
                actual = x.Actual,
            })
            .OrderBy(x => x.code, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.target, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.message, StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .ToArray() ?? Array.Empty<object>();
    }
}
