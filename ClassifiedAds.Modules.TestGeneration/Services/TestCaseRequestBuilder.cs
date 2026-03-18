using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Builds <see cref="TestCaseRequest"/> entities from n8n/LLM-generated test case data.
/// </summary>
public interface ITestCaseRequestBuilder
{
    /// <summary>
    /// Build a <see cref="TestCaseRequest"/> entity from n8n response data.
    /// </summary>
    TestCaseRequest Build(Guid testCaseId, N8nTestCaseRequest source, ApiOrderItemModel orderItem);
}

public class TestCaseRequestBuilder : ITestCaseRequestBuilder
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public TestCaseRequest Build(Guid testCaseId, N8nTestCaseRequest source, ApiOrderItemModel orderItem)
    {
        if (source == null)
        {
            // Fallback: build minimal request from order item metadata
            return new TestCaseRequest
            {
                Id = Guid.NewGuid(),
                TestCaseId = testCaseId,
                HttpMethod = ParseHttpMethod(orderItem?.HttpMethod),
                Url = orderItem?.Path ?? string.Empty,
                BodyType = BodyType.None,
                Timeout = 30000,
            };
        }

        return new TestCaseRequest
        {
            Id = Guid.NewGuid(),
            TestCaseId = testCaseId,
            HttpMethod = ParseHttpMethod(source.HttpMethod ?? orderItem?.HttpMethod),
            Url = source.Url ?? orderItem?.Path ?? string.Empty,
            Headers = SerializeDict(source.Headers),
            PathParams = SerializeDict(source.PathParams),
            QueryParams = SerializeDict(source.QueryParams),
            BodyType = ParseBodyType(source.BodyType),
            Body = source.Body,
            Timeout = source.Timeout ?? 30000,
        };
    }

    private static Entities.HttpMethod ParseHttpMethod(string method)
    {
        if (string.IsNullOrWhiteSpace(method)) return Entities.HttpMethod.GET;

        return method.Trim().ToUpperInvariant() switch
        {
            "GET" => Entities.HttpMethod.GET,
            "POST" => Entities.HttpMethod.POST,
            "PUT" => Entities.HttpMethod.PUT,
            "DELETE" => Entities.HttpMethod.DELETE,
            "PATCH" => Entities.HttpMethod.PATCH,
            "HEAD" => Entities.HttpMethod.HEAD,
            "OPTIONS" => Entities.HttpMethod.OPTIONS,
            _ => Entities.HttpMethod.GET,
        };
    }

    private static BodyType ParseBodyType(string bodyType)
    {
        if (string.IsNullOrWhiteSpace(bodyType)) return BodyType.None;

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
        if (dict == null || dict.Count == 0) return null;
        return JsonSerializer.Serialize(dict, JsonOpts);
    }
}
