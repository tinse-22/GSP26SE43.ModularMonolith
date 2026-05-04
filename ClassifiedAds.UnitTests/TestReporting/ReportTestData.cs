using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Models;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.UnitTests.TestReporting;

internal static class ReportTestData
{
    public static readonly Guid SuiteId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ProjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ApiSpecId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid RunId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid EndpointIdOrders = Guid.Parse("55555555-5555-5555-5555-555555555555");
    public static readonly Guid EndpointIdUsers = Guid.Parse("66666666-6666-6666-6666-666666666666");
    public static readonly Guid EndpointIdPayments = Guid.Parse("77777777-7777-7777-7777-777777777777");
    public static readonly Guid TestCaseIdOrders = Guid.Parse("88888888-8888-8888-8888-888888888888");
    public static readonly Guid TestCaseIdUsers = Guid.Parse("99999999-9999-9999-9999-999999999999");
    public static readonly Guid TestCaseIdPayments = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public const string ProjectName = "Checkout API";

    public static TestRunReportContextDto CreateContext()
    {
        return new TestRunReportContextDto
        {
            TestSuiteId = SuiteId,
            ProjectId = ProjectId,
            ProjectName = ProjectName,
            ApiSpecId = ApiSpecId,
            CreatedById = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            SuiteName = "Checkout Regression",
            Run = new TestRunReportRunDto
            {
                TestRunId = RunId,
                RunNumber = 5,
                EnvironmentId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
                TriggeredById = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"),
                Status = "Failed",
                StartedAt = new DateTimeOffset(2026, 3, 16, 12, 0, 0, TimeSpan.Zero),
                CompletedAt = new DateTimeOffset(2026, 3, 16, 12, 2, 30, TimeSpan.Zero),
                DurationMs = 150000,
                TotalTests = 3,
                PassedCount = 1,
                FailedCount = 1,
                SkippedCount = 1,
                ResolvedEnvironmentName = "Staging",
                ExecutedAt = new DateTimeOffset(2026, 3, 16, 12, 0, 0, TimeSpan.Zero),
            },
            RecentRuns = new[]
            {
                new TestRunHistoryItemDto
                {
                    TestRunId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                    RunNumber = 4,
                    Status = "Completed",
                    CompletedAt = new DateTimeOffset(2026, 3, 15, 12, 0, 0, TimeSpan.Zero),
                    DurationMs = 90000,
                    PassedCount = 3,
                    FailedCount = 0,
                    SkippedCount = 0,
                },
                new TestRunHistoryItemDto
                {
                    TestRunId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"),
                    RunNumber = 3,
                    Status = "Failed",
                    CompletedAt = new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero),
                    DurationMs = 130000,
                    PassedCount = 1,
                    FailedCount = 2,
                    SkippedCount = 0,
                },
            },
            OrderedEndpointIds = new[]
            {
                EndpointIdOrders,
                EndpointIdUsers,
                EndpointIdPayments,
            },
            Definitions = new[]
            {
                new ReportTestCaseDefinitionDto
                {
                    TestCaseId = TestCaseIdOrders,
                    EndpointId = EndpointIdOrders,
                    Name = "Create order",
                    Description = "Creates a checkout order",
                    TestType = "HappyPath",
                    OrderIndex = 1,
                    Request = new ExecutionTestCaseRequestDto
                    {
                        HttpMethod = "POST",
                        Url = "/api/orders",
                        Headers = "{\"Authorization\":\"Bearer raw-order-token\"}",
                        PathParams = "{}",
                        QueryParams = "{\"apiKey\":\"raw-order-key\"}",
                        BodyType = "Json",
                        Body = "{\"password\":\"raw-password\",\"amount\":120}",
                        Timeout = 30000,
                    },
                    Expectation = new ExecutionTestCaseExpectationDto
                    {
                        ExpectedStatus = "201",
                        ResponseSchema = "{\"type\":\"object\"}",
                        HeaderChecks = "{\"Set-Cookie\":\"session=abc\"}",
                        BodyContains = "[\"orderId\"]",
                        BodyNotContains = "[]",
                        JsonPathChecks = "{\"$.orderId\":\"exists\"}",
                        MaxResponseTime = 1000,
                    },
                },
                new ReportTestCaseDefinitionDto
                {
                    TestCaseId = TestCaseIdUsers,
                    EndpointId = EndpointIdUsers,
                    Name = "Get user",
                    Description = "Loads current user",
                    TestType = "HappyPath",
                    OrderIndex = 2,
                    Request = new ExecutionTestCaseRequestDto
                    {
                        HttpMethod = "GET",
                        Url = "/api/users/{id}",
                        Headers = "{\"Cookie\":\"session=secret-cookie\"}",
                        PathParams = "{\"id\":\"usr-001\"}",
                        QueryParams = "{}",
                        BodyType = "None",
                        Body = null,
                        Timeout = 30000,
                    },
                    Expectation = new ExecutionTestCaseExpectationDto
                    {
                        ExpectedStatus = "200",
                        ResponseSchema = "{\"type\":\"object\"}",
                        HeaderChecks = "{\"Authorization\":\"Bearer expected\"}",
                        BodyContains = "[\"id\"]",
                        BodyNotContains = "[]",
                        JsonPathChecks = "{\"$.id\":\"exists\"}",
                        MaxResponseTime = 1000,
                    },
                },
                new ReportTestCaseDefinitionDto
                {
                    TestCaseId = TestCaseIdPayments,
                    EndpointId = EndpointIdPayments,
                    Name = "List payments",
                    Description = "Lists payments",
                    TestType = "Coverage",
                    OrderIndex = 3,
                    Request = new ExecutionTestCaseRequestDto
                    {
                        HttpMethod = "GET",
                        Url = "/api/payments",
                        Headers = "{}",
                        PathParams = "{}",
                        QueryParams = "{}",
                        BodyType = "None",
                        Body = null,
                        Timeout = 30000,
                    },
                    Expectation = new ExecutionTestCaseExpectationDto
                    {
                        ExpectedStatus = "200",
                    },
                },
            },
            Results = new[]
            {
                new ReportTestCaseResultDto
                {
                    TestCaseId = TestCaseIdOrders,
                    EndpointId = EndpointIdOrders,
                    Name = "Create order",
                    OrderIndex = 1,
                    Status = "Passed",
                    HttpStatusCode = 201,
                    DurationMs = 430,
                    ResolvedUrl = "https://api.example.com/api/orders",
                    RequestHeaders = new Dictionary<string, string>
                    {
                        ["Authorization"] = "Bearer raw-order-token",
                        ["X-Trace"] = "trace-001",
                    },
                    ResponseHeaders = new Dictionary<string, string>
                    {
                        ["Set-Cookie"] = "session=secret-cookie",
                        ["Content-Type"] = "application/json",
                    },
                    ResponseBodyPreview = "{\"orderId\":\"ord-001\",\"apiKey\":\"secret-order-key\"}",
                    ExtractedVariables = new Dictionary<string, string>
                    {
                        ["orderId"] = "ord-001",
                        ["access_token"] = "raw-access-token",
                    },
                    FailureReasons = Array.Empty<ReportValidationFailureDto>(),
                    DependencyIds = Array.Empty<Guid>(),
                    SkippedBecauseDependencyIds = Array.Empty<Guid>(),
                    StatusCodeMatched = true,
                    SchemaMatched = true,
                    HeaderChecksPassed = true,
                    BodyContainsPassed = true,
                    BodyNotContainsPassed = true,
                    JsonPathChecksPassed = true,
                    ResponseTimePassed = true,
                },
                new ReportTestCaseResultDto
                {
                    TestCaseId = TestCaseIdUsers,
                    EndpointId = EndpointIdUsers,
                    Name = "Get user",
                    OrderIndex = 2,
                    Status = "Failed",
                    HttpStatusCode = 500,
                    DurationMs = 1200,
                    ResolvedUrl = "https://api.example.com/api/users/usr-001",
                    RequestHeaders = new Dictionary<string, string>
                    {
                        ["Cookie"] = "session=secret-cookie",
                    },
                    ResponseHeaders = new Dictionary<string, string>
                    {
                        ["Authorization"] = "Bearer leaked-response-token",
                    },
                    ResponseBodyPreview = "{\"message\":\"boom\",\"password\":\"raw-failure-password\",\"token\":\"raw-response-token\"} EXTRA",
                    ExtractedVariables = new Dictionary<string, string>
                    {
                        ["client_secret"] = "raw-client-secret",
                    },
                    FailureReasons = new[]
                    {
                        new ReportValidationFailureDto
                        {
                            Code = "STATUS_CODE_MISMATCH",
                            Message = "Expected 200 but got 500",
                            Target = "statusCode",
                            Expected = "200",
                            Actual = "500",
                        },
                    },
                    DependencyIds = new[] { TestCaseIdOrders },
                    SkippedBecauseDependencyIds = Array.Empty<Guid>(),
                    StatusCodeMatched = false,
                    SchemaMatched = false,
                    HeaderChecksPassed = false,
                    BodyContainsPassed = false,
                    BodyNotContainsPassed = true,
                    JsonPathChecksPassed = false,
                    ResponseTimePassed = false,
                },
            },
            Attempts = new[]
            {
                new TestRunExecutionAttemptDto
                {
                    ExecutionAttemptId = Guid.Parse("abababab-abab-abab-abab-abababababab"),
                    TestCaseId = TestCaseIdOrders,
                    AttemptNumber = 1,
                    Status = "Passed",
                    RetryReason = null,
                    SkippedCause = null,
                    DurationMs = 430,
                    StartedAt = new DateTimeOffset(2026, 3, 16, 12, 0, 0, TimeSpan.Zero),
                    CompletedAt = new DateTimeOffset(2026, 3, 16, 12, 0, 1, TimeSpan.Zero),
                    FailureReasons = Array.Empty<ReportValidationFailureDto>(),
                },
                new TestRunExecutionAttemptDto
                {
                    ExecutionAttemptId = Guid.Parse("bcbcbcbc-bcbc-bcbc-bcbc-bcbcbcbcbcbc"),
                    TestCaseId = TestCaseIdUsers,
                    AttemptNumber = 1,
                    Status = "Failed",
                    RetryReason = "token=raw-retry-token",
                    SkippedCause = "cookie=secret-cookie",
                    DurationMs = 1200,
                    StartedAt = new DateTimeOffset(2026, 3, 16, 12, 0, 2, TimeSpan.Zero),
                    CompletedAt = new DateTimeOffset(2026, 3, 16, 12, 0, 4, TimeSpan.Zero),
                    FailureReasons = new[]
                    {
                        new ReportValidationFailureDto
                        {
                            Code = "STATUS_CODE_MISMATCH",
                            Message = "Expected 200 but got 500",
                            Target = "statusCode",
                            Expected = "200",
                            Actual = "500",
                        },
                    },
                },
            },
        };
    }

    public static IReadOnlyList<ApiEndpointMetadataDto> CreateMetadata()
    {
        return new[]
        {
            new ApiEndpointMetadataDto
            {
                EndpointId = EndpointIdOrders,
                HttpMethod = "POST",
                Path = "/api/orders",
                OperationId = "createOrder",
            },
            new ApiEndpointMetadataDto
            {
                EndpointId = EndpointIdUsers,
                HttpMethod = "GET",
                Path = "/api/users/{id}",
                OperationId = "getUser",
            },
            new ApiEndpointMetadataDto
            {
                EndpointId = EndpointIdPayments,
                HttpMethod = "GET",
                Path = "/api/payments",
                OperationId = "listPayments",
            },
        };
    }

    public static CoverageMetricModel CreateCoverageModel()
    {
        return new CoverageMetricModel
        {
            TestRunId = RunId,
            TotalEndpoints = 3,
            TestedEndpoints = 2,
            CoveragePercent = 66.67m,
            ByMethod = new Dictionary<string, decimal>
            {
                ["GET"] = 50m,
                ["POST"] = 100m,
            },
            ByTag = new Dictionary<string, decimal>
            {
                ["orders"] = 100m,
                ["payments"] = 0m,
                ["users"] = 100m,
            },
            UncoveredPaths = new List<string>
            {
                "GET /api/payments",
            },
            CalculatedAt = new DateTimeOffset(2026, 3, 16, 12, 3, 0, TimeSpan.Zero),
        };
    }

    public static TestRunReportDocumentModel CreateDocument(ReportType reportType = ReportType.Detailed)
    {
        return new TestRunReportDocumentModel
        {
            TestSuiteId = SuiteId,
            ProjectId = ProjectId,
            ProjectName = ProjectName,
            ApiSpecId = ApiSpecId,
            SuiteName = "Checkout Regression",
            ReportType = reportType,
            GeneratedAt = new DateTimeOffset(2026, 3, 16, 12, 4, 0, TimeSpan.Zero),
            FileBaseName = $"test-run-5-{reportType.ToString().ToLowerInvariant()}-sample",
            Run = CreateContext().Run,
            Coverage = CreateCoverageModel(),
            FailureDistribution = new Dictionary<string, int>
            {
                ["STATUS_CODE_MISMATCH"] = 1,
            },
            RecentRuns = CreateContext().RecentRuns,
            Attempts = CreateContext().Attempts,
            Cases = new[]
            {
                new TestRunReportCaseDocumentModel
                {
                    TestCaseId = TestCaseIdOrders,
                    EndpointId = EndpointIdOrders,
                    Name = "Create order",
                    Description = "Creates a checkout order",
                    TestType = "HappyPath",
                    OrderIndex = 1,
                    Request = new ExecutionTestCaseRequestDto
                    {
                        HttpMethod = "POST",
                        Url = "/api/orders",
                        Headers = "{\"Authorization\":\"Bearer ***MASKED***\"}",
                        BodyType = "Json",
                        Body = "{\"password\":\"***MASKED***\",\"amount\":120}",
                    },
                    Status = "Passed",
                    HttpStatusCode = 201,
                    DurationMs = 430,
                    ResolvedUrl = "/api/orders",
                    RequestHeaders = new Dictionary<string, string>
                    {
                        ["Authorization"] = "***MASKED***",
                        ["X-Trace"] = "trace-001",
                    },
                    ResponseHeaders = new Dictionary<string, string>
                    {
                        ["Set-Cookie"] = "***MASKED***",
                    },
                    ResponseBodyPreview = "{\"orderId\":\"ord-001\",\"apiKey\":\"***MASKED***\"}",
                    ExtractedVariables = new Dictionary<string, string>
                    {
                        ["access_token"] = "***MASKED***",
                        ["orderId"] = "ord-001",
                    },
                    FailureReasons = Array.Empty<ReportValidationFailureDto>(),
                    DependencyIds = Array.Empty<Guid>(),
                    SkippedBecauseDependencyIds = Array.Empty<Guid>(),
                    StatusCodeMatched = true,
                },
                new TestRunReportCaseDocumentModel
                {
                    TestCaseId = TestCaseIdUsers,
                    EndpointId = EndpointIdUsers,
                    Name = "Get user",
                    Description = "Loads current user",
                    TestType = "HappyPath",
                    OrderIndex = 2,
                    Request = new ExecutionTestCaseRequestDto
                    {
                        HttpMethod = "GET",
                        Url = "/api/users/{id}",
                        Headers = "{\"Cookie\":\"***MASKED***\"}",
                    },
                    Expectation = new ExecutionTestCaseExpectationDto
                    {
                        ExpectedStatus = "200",
                        MaxResponseTime = 1000,
                    },
                    Status = "Failed",
                    HttpStatusCode = 500,
                    DurationMs = 1200,
                    ResolvedUrl = "/api/users/usr-001",
                    RequestHeaders = new Dictionary<string, string>
                    {
                        ["Cookie"] = "***MASKED***",
                    },
                    ResponseHeaders = new Dictionary<string, string>
                    {
                        ["Authorization"] = "***MASKED***",
                    },
                    ResponseBodyPreview = "{\"message\":\"boom\",\"token\":\"***MASKED***\"}",
                    ExtractedVariables = new Dictionary<string, string>
                    {
                        ["client_secret"] = "***MASKED***",
                    },
                    FailureReasons = new[]
                    {
                        new ReportValidationFailureDto
                        {
                            Code = "STATUS_CODE_MISMATCH",
                            Message = "Expected 200 but got 500",
                        },
                    },
                    DependencyIds = new[] { TestCaseIdOrders },
                    SkippedBecauseDependencyIds = Array.Empty<Guid>(),
                    StatusCodeMatched = false,
                },
            },
        };
    }
}
