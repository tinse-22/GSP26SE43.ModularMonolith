using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Builds <see cref="TestCaseRequest"/> entities from n8n/LLM-generated test case data.
/// </summary>
public interface ITestCaseRequestBuilder
{
    /// <summary>
    /// Build a <see cref="TestCaseRequest"/> entity from n8n response data.
    /// </summary>
    /// <returns></returns>
    TestCaseRequest Build(Guid testCaseId, N8nTestCaseRequest source, ApiOrderItemModel orderItem);
}

public class TestCaseRequestBuilder : ITestCaseRequestBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private static readonly Regex HttpMethodTokenRegex = new (
        @"(?<![A-Za-z])(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public TestCaseRequest Build(Guid testCaseId, N8nTestCaseRequest source, ApiOrderItemModel orderItem)
    {
        if (source == null)
        {
            // Fallback: build minimal request from order item metadata
            return new TestCaseRequest
            {
                Id = Guid.NewGuid(),
                TestCaseId = testCaseId,
                HttpMethod = ResolveHttpMethod(null, orderItem?.HttpMethod),
                Url = orderItem?.Path ?? string.Empty,
                BodyType = BodyType.None,
                Timeout = 30000,
            };
        }

        return new TestCaseRequest
        {
            Id = Guid.NewGuid(),
            TestCaseId = testCaseId,
            HttpMethod = ResolveHttpMethod(source.HttpMethod, orderItem?.HttpMethod),
            Url = source.Url ?? orderItem?.Path ?? string.Empty,
            Headers = SerializeDict(source.Headers),
            PathParams = SerializeDict(source.PathParams),
            QueryParams = SerializeDict(source.QueryParams),
            BodyType = ParseBodyType(source.BodyType),
            Body = source.Body,
            Timeout = source.Timeout ?? 30000,
        };
    }

    private static Entities.HttpMethod ResolveHttpMethod(string preferredMethod, string fallbackMethod)
    {
        if (TryParseHttpMethod(preferredMethod, out var parsed))
        {
            return parsed;
        }

        if (TryParseHttpMethod(fallbackMethod, out parsed))
        {
            return parsed;
        }

        return Entities.HttpMethod.GET;
    }

    private static bool TryParseHttpMethod(string method, out Entities.HttpMethod parsed)
    {
        parsed = Entities.HttpMethod.GET;

        if (string.IsNullOrWhiteSpace(method))
        {
            return false;
        }

        if (MapHttpMethod(method, out parsed))
        {
            return true;
        }

        var match = HttpMethodTokenRegex.Match(method);
        return match.Success && MapHttpMethod(match.Groups[1].Value, out parsed);
    }

    private static bool MapHttpMethod(string method, out Entities.HttpMethod parsed)
    {
        switch (method?.Trim().ToUpperInvariant())
        {
            case "GET":
                parsed = Entities.HttpMethod.GET;
                return true;
            case "POST":
                parsed = Entities.HttpMethod.POST;
                return true;
            case "PUT":
                parsed = Entities.HttpMethod.PUT;
                return true;
            case "DELETE":
                parsed = Entities.HttpMethod.DELETE;
                return true;
            case "PATCH":
                parsed = Entities.HttpMethod.PATCH;
                return true;
            case "HEAD":
                parsed = Entities.HttpMethod.HEAD;
                return true;
            case "OPTIONS":
                parsed = Entities.HttpMethod.OPTIONS;
                return true;
            default:
                parsed = Entities.HttpMethod.GET;
                return false;
        }
    }

    private static BodyType ParseBodyType(string bodyType)
    {
        if (string.IsNullOrWhiteSpace(bodyType))
        {
            return BodyType.None;
        }

        return bodyType.Trim().ToUpperInvariant() switch
        {
            "JSON" => BodyType.JSON,
            "FORMDATA" or "FORM_DATA" => BodyType.FormData,
            "URLENCODED" or "URL_ENCODED" => BodyType.UrlEncoded,
            "RAW" => BodyType.Raw,
            "BINARY" => BodyType.Binary,
            _ => BodyType.None,
        };
    }

    private static string SerializeDict(Dictionary<string, string> dict)
    {
        if (dict == null || dict.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(dict, JsonOpts);
    }
}
