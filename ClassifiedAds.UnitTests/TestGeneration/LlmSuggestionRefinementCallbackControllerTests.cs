using ClassifiedAds.Modules.TestGeneration.Controllers;
using System;
using System.Text.Json;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class LlmSuggestionRefinementCallbackControllerTests
{
    [Fact]
    public void TryParseCallbackPayload_Should_AcceptScenariosShape()
    {
        using var document = JsonDocument.Parse("""
        {
          "scenarios": [
            {
              "endpointId": "11111111-1111-1111-1111-111111111111",
              "scenarioName": "Missing required email",
              "description": "Rejects empty email",
              "testType": "Negative",
              "priority": "High",
              "request": { "httpMethod": "POST", "url": "/api/auth/register", "bodyType": "JSON" },
              "expectation": { "expectedStatus": [400], "bodyContains": ["email"] }
            }
          ],
          "model": "gpt-4.1-mini",
          "tokensUsed": 123
        }
        """);

        var parsed = LlmSuggestionRefinementCallbackController.TryParseCallbackPayload(
            document.RootElement,
            out var response,
            out var error);

        parsed.Should().BeTrue(error);
        response.Scenarios.Should().HaveCount(1);
        response.Scenarios[0].ScenarioName.Should().Be("Missing required email");
        response.Model.Should().Be("gpt-4.1-mini");
        response.TokensUsed.Should().Be(123);
    }

    [Fact]
    public void TryParseCallbackPayload_Should_MapTestCasesShape_ToScenarios()
    {
        using var document = JsonDocument.Parse("""
        {
          "testCases": [
            {
              "endpointId": "22222222-2222-2222-2222-222222222222",
              "name": "Duplicate registration email",
              "description": "Rejects duplicate email",
              "testType": "Negative",
              "priority": "Critical",
              "tags": ["llm-suggested"],
              "request": { "httpMethod": "POST", "url": "/api/auth/register", "bodyType": "JSON" },
              "expectation": { "expectedStatus": [409], "bodyContains": ["already exists"] }
            }
          ],
          "model": "gpt-4.1-mini",
          "tokensUsed": "456"
        }
        """);

        var parsed = LlmSuggestionRefinementCallbackController.TryParseCallbackPayload(
            document.RootElement,
            out var response,
            out var error);

        parsed.Should().BeTrue(error);
        response.Scenarios.Should().HaveCount(1);
        response.Scenarios[0].EndpointId.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        response.Scenarios[0].ScenarioName.Should().Be("Duplicate registration email");
        response.Scenarios[0].TestType.Should().Be("Negative");
        response.Scenarios[0].Expectation.ExpectedStatus.Should().ContainSingle().Which.Should().Be(409);
        response.TokensUsed.Should().Be(456);
    }

    [Fact]
    public void TryParseCallbackPayload_Should_UnwrapN8nBodyString()
    {
        using var document = JsonDocument.Parse("""
        {
          "body": "{\"testCases\":[{\"endpointId\":\"33333333-3333-3333-3333-333333333333\",\"name\":\"Boundary page size\",\"testType\":\"Boundary\",\"request\":{\"httpMethod\":\"GET\",\"url\":\"/api/items\"},\"expectation\":{\"expectedStatus\":[400]}}]}"
        }
        """);

        var parsed = LlmSuggestionRefinementCallbackController.TryParseCallbackPayload(
            document.RootElement,
            out var response,
            out var error);

        parsed.Should().BeTrue(error);
        response.Scenarios.Should().HaveCount(1);
        response.Scenarios[0].ScenarioName.Should().Be("Boundary page size");
        response.Scenarios[0].TestType.Should().Be("Boundary");
    }

    [Fact]
    public void TryParseCallbackPayload_Should_RejectUnknownShape()
    {
        using var document = JsonDocument.Parse("""{ "message": "done" }""");

        var parsed = LlmSuggestionRefinementCallbackController.TryParseCallbackPayload(
            document.RootElement,
            out var response,
            out var error);

        parsed.Should().BeFalse();
        response.Should().BeNull();
        error.Should().Contain("scenarios or testCases");
    }
}
