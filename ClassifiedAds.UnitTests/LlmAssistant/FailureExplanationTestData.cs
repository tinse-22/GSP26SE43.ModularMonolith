using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.LlmAssistant.ConfigurationOptions;
using ClassifiedAds.Modules.LlmAssistant.Models;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ClassifiedAds.UnitTests.LlmAssistant;

internal static class FailureExplanationTestData
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static TestFailureExplanationContextDto CreateContext(
        string secret = "super-secret-value",
        Guid? suiteId = null,
        Guid? apiSpecId = null,
        Guid? ownerId = null,
        Guid? runId = null,
        Guid? endpointId = null,
        Guid? testCaseId = null)
    {
        var resolvedSuiteId = suiteId ?? Guid.Parse("11111111-1111-1111-1111-111111111111");
        var resolvedApiSpecId = apiSpecId ?? Guid.Parse("22222222-2222-2222-2222-222222222222");
        var resolvedOwnerId = ownerId ?? Guid.Parse("33333333-3333-3333-3333-333333333333");
        var resolvedRunId = runId ?? Guid.Parse("44444444-4444-4444-4444-444444444444");
        var resolvedEndpointId = endpointId ?? Guid.Parse("55555555-5555-5555-5555-555555555555");
        var resolvedTestCaseId = testCaseId ?? Guid.Parse("66666666-6666-6666-6666-666666666666");

        return new TestFailureExplanationContextDto
        {
            TestSuiteId = resolvedSuiteId,
            ProjectId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            ApiSpecId = resolvedApiSpecId,
            CreatedById = resolvedOwnerId,
            TestRunId = resolvedRunId,
            RunNumber = 9,
            TriggeredById = Guid.Parse("88888888-8888-8888-8888-888888888888"),
            ResolvedEnvironmentName = "Staging",
            ExecutedAt = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero),
            Definition = new FailureExplanationDefinitionDto
            {
                TestCaseId = resolvedTestCaseId,
                EndpointId = resolvedEndpointId,
                Name = "Create order",
                Description = "Create order should return 201.",
                TestType = "HappyPath",
                OrderIndex = 2,
                DependencyIds = new[]
                {
                    Guid.Parse("99999999-9999-9999-9999-999999999999"),
                },
                Request = new ExecutionTestCaseRequestDto
                {
                    HttpMethod = "POST",
                    Url = $"/api/orders?apiKey={secret}",
                    Headers = $"{{\"Authorization\":\"Bearer {secret}\",\"apiKey\":\"{secret}\"}}",
                    PathParams = $"{{\"token\":\"{secret}\"}}",
                    QueryParams = $"{{\"password\":\"{secret}\",\"apiKey\":\"{secret}\"}}",
                    BodyType = "Json",
                    Body = $"{{\"token\":\"{secret}\",\"password\":\"{secret}\",\"apiKey\":\"{secret}\"}}",
                    Timeout = 30000,
                },
                Expectation = new ExecutionTestCaseExpectationDto
                {
                    ExpectedStatus = "201",
                    ResponseSchema = "{\"type\":\"object\"}",
                    HeaderChecks = $"{{\"Authorization\":\"Bearer {secret}\"}}",
                    BodyContains = $"[\"token={secret}\"]",
                    BodyNotContains = "[]",
                    JsonPathChecks = $"{{\"$.data.apiKey\":\"{secret}\"}}",
                    MaxResponseTime = 1000,
                },
            },
            ActualResult = new FailureExplanationActualResultDto
            {
                Status = "Failed",
                HttpStatusCode = 500,
                DurationMs = 432,
                ResolvedUrl = $"/api/orders?token={secret}&apiKey={secret}",
                RequestHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {secret}",
                    ["Cookie"] = $"session={secret}",
                    ["X-Trace-Id"] = "trace-001",
                },
                ResponseHeaders = new Dictionary<string, string>
                {
                    ["Set-Cookie"] = $"refresh={secret}",
                    ["X-Request-Id"] = "request-001",
                },
                ResponseBodyPreview = $"{{\"token\":\"{secret}\",\"password\":\"{secret}\",\"apiKey\":\"{secret}\"}}",
                FailureReasons = new List<FailureExplanationFailureReasonDto>
                {
                    new()
                    {
                        Code = "STATUS_CODE_MISMATCH",
                        Message = $"Expected 201 but token={secret} returned 500.",
                        Target = "statusCode",
                        Expected = "201",
                        Actual = "500",
                    },
                },
                ExtractedVariables = new Dictionary<string, string>
                {
                    ["authToken"] = secret,
                    ["password"] = secret,
                    ["apiKey"] = secret,
                },
                DependencyIds = new[]
                {
                    Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                },
                SkippedBecauseDependencyIds = Array.Empty<Guid>(),
                StatusCodeMatched = false,
                SchemaMatched = false,
                HeaderChecksPassed = false,
                BodyContainsPassed = false,
                BodyNotContainsPassed = true,
                JsonPathChecksPassed = false,
                ResponseTimePassed = true,
            },
        };
    }

    public static ApiEndpointMetadataDto CreateEndpointMetadata(Guid? endpointId = null)
    {
        return new ApiEndpointMetadataDto
        {
            EndpointId = endpointId ?? Guid.Parse("55555555-5555-5555-5555-555555555555"),
            HttpMethod = "POST",
            Path = "/api/orders",
            OperationId = "CreateOrder",
            IsAuthRelated = false,
            ParameterSchemaPayloads = new[] { "{\"type\":\"object\",\"required\":[\"sku\"]}" },
            ResponseSchemaPayloads = new[] { "{\"type\":\"object\",\"required\":[\"id\"]}" },
        };
    }

    public static FailureExplanationProviderResponse CreateProviderResponse(
        string summaryVi = "Loi do backend tra ve 500.")
    {
        return new FailureExplanationProviderResponse
        {
            SummaryVi = summaryVi,
            PossibleCauses = new[] { "Dich vu tao don hang gap loi." },
            SuggestedNextActions = new[] { "Kiem tra log backend." },
            Confidence = "High",
            Model = "gpt-4.1-mini",
            TokensUsed = 123,
        };
    }

    public static FailureExplanationPrompt CreatePrompt(
        TestFailureExplanationContextDto context,
        ApiEndpointMetadataDto? endpointMetadata = null,
        string promptText = "Explain the failed request.")
    {
        return new FailureExplanationPrompt
        {
            Provider = "N8n",
            Model = "gpt-4.1-mini",
            Prompt = promptText,
            SanitizedContextJson = JsonSerializer.Serialize(
                new
                {
                    context,
                    endpointMetadata,
                },
                JsonOptions),
            SanitizedContext = context,
            EndpointMetadata = endpointMetadata,
        };
    }

    public static IOptions<LlmAssistantModuleOptions> CreateOptions(
        string provider = "N8n",
        string model = "gpt-4.1-mini",
        int cacheTtlHours = 24)
    {
        return Options.Create(new LlmAssistantModuleOptions
        {
            FailureExplanation = new FailureExplanationOptions
            {
                Provider = provider,
                Model = model,
                CacheTtlHours = cacheTtlHours,
            },
        });
    }
}
