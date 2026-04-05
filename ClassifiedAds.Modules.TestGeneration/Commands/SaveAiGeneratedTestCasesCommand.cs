using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

/// <summary>Callback payload posted by n8n after LLM test-case generation.</summary>
public class AiTestCaseRequestDto
{
    public string HttpMethod { get; set; } = "GET";
    public string Url { get; set; }
    public string Headers { get; set; }
    public string PathParams { get; set; }
    public string QueryParams { get; set; }
    public string BodyType { get; set; } = "None";
    public string Body { get; set; }
    public int Timeout { get; set; } = 30000;
}

public class AiTestCaseExpectationDto
{
    public string ExpectedStatus { get; set; } = "[200]";
    public string ResponseSchema { get; set; }
    public string HeaderChecks { get; set; }
    public string BodyContains { get; set; }
    public string BodyNotContains { get; set; }
    public string JsonPathChecks { get; set; }
    public int? MaxResponseTime { get; set; }
}

public class AiGeneratedTestCaseDto
{
    public Guid? EndpointId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    /// <summary>e.g. "HappyPath", "Boundary", "Negative", "Performance", "Security"</summary>
    public string TestType { get; set; } = "HappyPath";
    /// <summary>e.g. "Critical", "High", "Medium", "Low"</summary>
    public string Priority { get; set; } = "Medium";
    public int OrderIndex { get; set; }
    public string Tags { get; set; }
    public AiTestCaseRequestDto Request { get; set; }
    public AiTestCaseExpectationDto Expectation { get; set; }
}

// ─────────────────────────────────────────────────────────────────
public class SaveAiGeneratedTestCasesCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public List<AiGeneratedTestCaseDto> TestCases { get; set; } = new();
}

public class SaveAiGeneratedTestCasesCommandHandler : ICommandHandler<SaveAiGeneratedTestCasesCommand>
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseRequest, Guid> _testCaseRequestRepository;
    private readonly IRepository<TestCaseExpectation, Guid> _testCaseExpectationRepository;
    private readonly IRepository<TestSuiteVersion, Guid> _versionRepository;
    private readonly ILogger<SaveAiGeneratedTestCasesCommandHandler> _logger;

    public SaveAiGeneratedTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseRequest, Guid> testCaseRequestRepository,
        IRepository<TestCaseExpectation, Guid> testCaseExpectationRepository,
        IRepository<TestSuiteVersion, Guid> versionRepository,
        ILogger<SaveAiGeneratedTestCasesCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _testCaseRequestRepository = testCaseRequestRepository;
        _testCaseExpectationRepository = testCaseExpectationRepository;
        _versionRepository = versionRepository;
        _logger = logger;
    }

    public async Task HandleAsync(SaveAiGeneratedTestCasesCommand command, CancellationToken cancellationToken = default)
    {
        if (command.TestSuiteId == Guid.Empty)
            throw new ValidationException("TestSuiteId is required.");

        if (command.TestCases == null || command.TestCases.Count == 0)
            throw new ValidationException("At least one AI-generated test case is required.");

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
            throw new NotFoundException($"Test suite '{command.TestSuiteId}' was not found.");

        if (suite.Status == TestSuiteStatus.Archived)
            throw new ValidationException("Cannot save AI-generated test cases for an archived test suite.");

        await _testCaseRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Replace any previously AI-generated test cases for this suite.
            var existing = await _testCaseRepository.ToListAsync(
                _testCaseRepository.GetQueryableSet()
                    .Where(x => x.TestSuiteId == command.TestSuiteId));

            if (existing.Count > 0)
            {
                await _testCaseRepository.BulkDeleteAsync(existing, ct);
            }

            // Create new entities from the n8n payload.
            var now = DateTimeOffset.UtcNow;
            var actorUserId = suite.LastModifiedById ?? suite.CreatedById;
            var persistedTestCases = new List<TestCase>(command.TestCases.Count);
            var orderIdx = 0;
            foreach (var dto in command.TestCases)
            {
                var testCase = new TestCase
                {
                    Id = Guid.NewGuid(),
                    TestSuiteId = command.TestSuiteId,
                    EndpointId = dto.EndpointId,
                    Name = dto.Name ?? $"Test {orderIdx + 1}",
                    Description = dto.Description,
                    TestType = ParseTestType(dto.TestType),
                    Priority = ParsePriority(dto.Priority),
                    IsEnabled = true,
                    OrderIndex = dto.OrderIndex > 0 ? dto.OrderIndex : orderIdx,
                    Tags = NormalizeTagsJson(dto.Tags),
                    Version = 1,
                    LastModifiedById = actorUserId,
                    CreatedDateTime = now,
                };

                await _testCaseRepository.AddAsync(testCase, ct);
                persistedTestCases.Add(testCase);

                if (dto.Request is not null)
                {
                    var req = new TestCaseRequest
                    {
                        Id = Guid.NewGuid(),
                        TestCaseId = testCase.Id,
                        HttpMethod = ParseHttpMethod(dto.Request.HttpMethod),
                        Url = dto.Request.Url,
                        Headers = NormalizeJsonOrDefault(dto.Request.Headers, "{}"),
                        PathParams = NormalizeJsonOrDefault(dto.Request.PathParams, "{}"),
                        QueryParams = NormalizeJsonOrDefault(dto.Request.QueryParams, "{}"),
                        BodyType = ParseBodyType(dto.Request.BodyType),
                        Body = dto.Request.Body,
                        Timeout = dto.Request.Timeout > 0 ? dto.Request.Timeout : 30000,
                        CreatedDateTime = now,
                    };
                    await _testCaseRequestRepository.AddAsync(req, ct);
                }

                if (dto.Expectation is not null)
                {
                    var exp = new TestCaseExpectation
                    {
                        Id = Guid.NewGuid(),
                        TestCaseId = testCase.Id,
                        ExpectedStatus = NormalizeJsonOrDefault(dto.Expectation.ExpectedStatus, "[200]"),
                        ResponseSchema = NormalizeNullableJson(dto.Expectation.ResponseSchema),
                        HeaderChecks = NormalizeJsonOrDefault(dto.Expectation.HeaderChecks, "{}"),
                        BodyContains = NormalizeJsonOrDefault(dto.Expectation.BodyContains, "[]"),
                        BodyNotContains = NormalizeJsonOrDefault(dto.Expectation.BodyNotContains, "[]"),
                        JsonPathChecks = NormalizeJsonOrDefault(dto.Expectation.JsonPathChecks, "{}"),
                        MaxResponseTime = dto.Expectation.MaxResponseTime,
                        CreatedDateTime = now,
                    };
                    await _testCaseExpectationRepository.AddAsync(exp, ct);
                }

                orderIdx++;
            }

            await _versionRepository.AddAsync(new TestSuiteVersion
            {
                Id = Guid.NewGuid(),
                TestSuiteId = command.TestSuiteId,
                VersionNumber = suite.Version + 1,
                ChangedById = actorUserId,
                ChangeType = VersionChangeType.TestCasesModified,
                ChangeDescription = $"Saved {persistedTestCases.Count} AI-generated test case(s) from n8n callback.",
                TestCaseOrderSnapshot = JsonSerializer.Serialize(
                    persistedTestCases
                        .OrderBy(tc => tc.OrderIndex)
                        .Select(tc => new { tc.Id, tc.EndpointId, tc.Name, tc.OrderIndex, tc.TestType })
                        .ToList(),
                    JsonOpts),
                ApprovalStatusSnapshot = suite.ApprovalStatus,
                CreatedDateTime = now,
            }, ct);

            suite.Version += 1;
            suite.Status = TestSuiteStatus.Ready;
            suite.LastModifiedById = actorUserId;
            suite.UpdatedDateTime = now;
            suite.RowVersion = Guid.NewGuid().ToByteArray();
            await _suiteRepository.UpdateAsync(suite, ct);

            await _testCaseRepository.UnitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Saved {Count} AI-generated test cases and marked suite Ready for TestSuiteId={TestSuiteId}",
                command.TestCases.Count,
                command.TestSuiteId);
        }, cancellationToken: cancellationToken);
    }

    private static TestType ParseTestType(string value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "happypath" or "happy_path" or "happy path" => TestType.HappyPath,
            "boundary" => TestType.Boundary,
            "negative" => TestType.Negative,
            "performance" => TestType.Performance,
            "security" => TestType.Security,
            _ => TestType.HappyPath,
        };

    private static TestPriority ParsePriority(string value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "critical" => TestPriority.Critical,
            "high" => TestPriority.High,
            "medium" or "normal" => TestPriority.Medium,
            "low" => TestPriority.Low,
            _ => TestPriority.Medium,
        };

    private static HttpMethod ParseHttpMethod(string value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "POST" => HttpMethod.POST,
            "PUT" => HttpMethod.PUT,
            "DELETE" => HttpMethod.DELETE,
            "PATCH" => HttpMethod.PATCH,
            _ => HttpMethod.GET,
        };

    private static BodyType ParseBodyType(string value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "json" => BodyType.JSON,
            "formdata" or "form_data" or "form-data" => BodyType.FormData,
            "urlencoded" or "url_encoded" or "url-encoded" => BodyType.UrlEncoded,
            "raw" => BodyType.Raw,
            _ => BodyType.None,
        };

    private static string NormalizeTagsJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[]";
        }

        var trimmed = value.Trim();
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? trimmed
                : JsonSerializer.Serialize(new[] { trimmed });
        }
        catch
        {
            var parts = trimmed
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();

            return JsonSerializer.Serialize(parts.Length > 0 ? parts : new[] { trimmed });
        }
    }

    private static string NormalizeJsonOrDefault(string value, string fallbackJson)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallbackJson;
        }

        var trimmed = value.Trim();
        try
        {
            using var _ = JsonDocument.Parse(trimmed);
            return trimmed;
        }
        catch
        {
            return JsonSerializer.Serialize(trimmed);
        }
    }

    private static string NormalizeNullableJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        try
        {
            using var _ = JsonDocument.Parse(trimmed);
            return trimmed;
        }
        catch
        {
            return JsonSerializer.Serialize(trimmed);
        }
    }
}
