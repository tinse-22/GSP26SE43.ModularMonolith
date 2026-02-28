using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;
using HttpMethodEnum = ClassifiedAds.Modules.TestGeneration.Entities.HttpMethod;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// FE-05B: TestCaseRequestBuilder unit tests.
/// Verifies HTTP method parsing, body type mapping, path/query/header serialization.
/// </summary>
public class TestCaseRequestBuilderTests
{
    private readonly TestCaseRequestBuilder _builder = new();

    [Fact]
    public void Build_Should_MapAllFields_FromN8nTestCaseRequest()
    {
        var testCaseId = Guid.NewGuid();
        var source = new N8nTestCaseRequest
        {
            HttpMethod = "POST",
            Url = "/api/users",
            Headers = new Dictionary<string, string> { { "Authorization", "Bearer {{token}}" } },
            PathParams = new Dictionary<string, string>(),
            QueryParams = new Dictionary<string, string> { { "page", "1" } },
            BodyType = "JSON",
            Body = "{\"name\":\"test\"}",
            Timeout = 15000,
        };
        var orderItem = new ApiOrderItemModel { HttpMethod = "POST", Path = "/api/users" };

        var result = _builder.Build(testCaseId, source, orderItem);

        result.TestCaseId.Should().Be(testCaseId);
        result.HttpMethod.Should().Be(HttpMethodEnum.POST);
        result.Url.Should().Be("/api/users");
        result.Headers.Should().Contain("Authorization");
        result.QueryParams.Should().Contain("page");
        result.BodyType.Should().Be(BodyType.JSON);
        result.Body.Should().Be("{\"name\":\"test\"}");
        result.Timeout.Should().Be(15000);
    }

    [Fact]
    public void Build_Should_UseFallback_WhenSourceIsNull()
    {
        var testCaseId = Guid.NewGuid();
        var orderItem = new ApiOrderItemModel { HttpMethod = "GET", Path = "/api/items" };

        var result = _builder.Build(testCaseId, null, orderItem);

        result.TestCaseId.Should().Be(testCaseId);
        result.HttpMethod.Should().Be(HttpMethodEnum.GET);
        result.Url.Should().Be("/api/items");
        result.BodyType.Should().Be(BodyType.None);
        result.Timeout.Should().Be(30000);
    }

    [Theory]
    [InlineData("GET", HttpMethodEnum.GET)]
    [InlineData("POST", HttpMethodEnum.POST)]
    [InlineData("PUT", HttpMethodEnum.PUT)]
    [InlineData("DELETE", HttpMethodEnum.DELETE)]
    [InlineData("PATCH", HttpMethodEnum.PATCH)]
    [InlineData("head", HttpMethodEnum.HEAD)]
    [InlineData("options", HttpMethodEnum.OPTIONS)]
    [InlineData("UNKNOWN", HttpMethodEnum.GET)]
    [InlineData("", HttpMethodEnum.GET)]
    [InlineData(null, HttpMethodEnum.GET)]
    public void Build_Should_ParseHttpMethodCorrectly(string input, HttpMethodEnum expected)
    {
        var source = new N8nTestCaseRequest { HttpMethod = input, Url = "/test" };
        var result = _builder.Build(Guid.NewGuid(), source, null);

        result.HttpMethod.Should().Be(expected);
    }

    [Theory]
    [InlineData("JSON", BodyType.JSON)]
    [InlineData("FormData", BodyType.FormData)]
    [InlineData("FORM_DATA", BodyType.FormData)]
    [InlineData("UrlEncoded", BodyType.UrlEncoded)]
    [InlineData("Raw", BodyType.Raw)]
    [InlineData("Binary", BodyType.Binary)]
    [InlineData("UNKNOWN", BodyType.None)]
    [InlineData(null, BodyType.None)]
    public void Build_Should_ParseBodyTypeCorrectly(string input, BodyType expected)
    {
        var source = new N8nTestCaseRequest { BodyType = input, Url = "/test" };
        var result = _builder.Build(Guid.NewGuid(), source, null);

        result.BodyType.Should().Be(expected);
    }

    [Fact]
    public void Build_Should_SetDefaultTimeout_WhenNullInSource()
    {
        var source = new N8nTestCaseRequest { Url = "/test", Timeout = null };
        var result = _builder.Build(Guid.NewGuid(), source, null);

        result.Timeout.Should().Be(30000);
    }
}
