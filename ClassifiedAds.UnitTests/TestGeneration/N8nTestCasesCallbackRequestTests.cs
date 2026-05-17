using ClassifiedAds.Modules.TestGeneration.Controllers;
using System.Text.Json;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class N8nTestCasesCallbackRequestTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    [Fact]
    public void Deserialize_Should_AcceptRawJsonFields_FromN8nCallback()
    {
        const string json = """
        {
          "testCases": [
            {
              "endpointId": "11111111-1111-1111-1111-111111111111",
              "name": "Create item",
              "description": "Creates an item",
              "testType": "HappyPath",
              "priority": "High",
              "orderIndex": 0,
              "tags": ["happy-path", "llm-suggested"],
              "request": {
                "httpMethod": "POST",
                "url": "/api/items",
                "headers": { "Content-Type": "application/json" },
                "pathParams": { "tenantId": "{{tenantId}}" },
                "queryParams": { "include": "details" },
                "bodyType": "JSON",
                "body": { "name": "Item {{tcUniqueId}}" },
                "timeout": 30000
              },
              "expectation": {
                "expectedStatus": [200, "201"],
                "responseSchema": { "type": "object" },
                "headerChecks": { "Location": "exists" },
                "bodyContains": ["id", "name"],
                "bodyNotContains": ["error"],
                "jsonPathChecks": { "$.id": "not_null" },
                "maxResponseTime": 1500
              },
              "variables": [],
              "coveredRequirementIds": [],
              "traceabilityScore": 0.9,
              "mappingRationale": "Covers item creation."
            }
          ]
        }
        """;

        var request = JsonSerializer.Deserialize<N8nTestCasesCallbackRequest>(json, JsonOptions);

        request.Should().NotBeNull();
        request!.TestCases.Should().ContainSingle();

        var testCase = request.TestCases[0];
        testCase.Tags.Should().Be("""["happy-path", "llm-suggested"]""");
        testCase.Request.Headers.Should().Be("""{ "Content-Type": "application/json" }""");
        testCase.Request.PathParams.Should().Be("""{ "tenantId": "{{tenantId}}" }""");
        testCase.Request.QueryParams.Should().Be("""{ "include": "details" }""");
        testCase.Request.Body.Should().Be("""{ "name": "Item {{tcUniqueId}}" }""");
        testCase.Expectation.ExpectedStatus.Should().Be("[200,201]");
        testCase.Expectation.ResponseSchema.Should().Be("""{ "type": "object" }""");
        testCase.Expectation.HeaderChecks.Should().Be("""{ "Location": "exists" }""");
        testCase.Expectation.BodyContains.Should().Be("""["id", "name"]""");
        testCase.Expectation.BodyNotContains.Should().Be("""["error"]""");
        testCase.Expectation.JsonPathChecks.Should().Be("""{ "$.id": "not_null" }""");
    }

    [Fact]
    public void Deserialize_Should_KeepStringifiedJsonFields_WhenAlreadyNormalized()
    {
        const string json = """
        {
          "testCases": [
            {
              "name": "Get items",
              "tags": "[\"happy-path\",\"auto-generated\"]",
              "request": {
                "httpMethod": "GET",
                "url": "/api/items",
                "headers": "{\"Accept\":\"application/json\"}"
              },
              "expectation": {
                "expectedStatus": "[200]",
                "bodyContains": "[\"items\"]"
              }
            }
          ]
        }
        """;

        var request = JsonSerializer.Deserialize<N8nTestCasesCallbackRequest>(json, JsonOptions);

        request.Should().NotBeNull();
        var testCase = request!.TestCases[0];
        testCase.Tags.Should().Be("""["happy-path","auto-generated"]""");
        testCase.Request.Headers.Should().Be("""{"Accept":"application/json"}""");
        testCase.Expectation.ExpectedStatus.Should().Be("[200]");
        testCase.Expectation.BodyContains.Should().Be("""["items"]""");
    }
}
