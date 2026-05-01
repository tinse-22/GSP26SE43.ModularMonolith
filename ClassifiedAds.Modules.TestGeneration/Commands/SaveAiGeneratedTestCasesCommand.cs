using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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

public class AiTestCaseVariableDto
{
    public string VariableName { get; set; }
    public string ExtractFrom { get; set; }
    public string JsonPath { get; set; }
    public string HeaderName { get; set; }
    public string Regex { get; set; }
    public string DefaultValue { get; set; }
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
    public List<AiTestCaseVariableDto> Variables { get; set; } = new();

    /// <summary>
    /// SRS requirement IDs that this test case covers.
    /// Populated by the LLM when the generation payload includes SRS requirements context.
    /// </summary>
    public List<Guid> CoveredRequirementIds { get; set; } = new();

    /// <summary>
    /// LLM-provided traceability score (0.0–1.0) for the primary covered requirement.
    /// If null, defaults to 1.0 when saving.
    /// </summary>
    public float? TraceabilityScore { get; set; }

    /// <summary>
    /// LLM-generated rationale explaining why this test case covers the linked requirements.
    /// If null, a default rationale is used.
    /// </summary>
    public string MappingRationale { get; set; }
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

    private static readonly Regex HttpMethodTokenRegex = new(
        @"(?<![A-Za-z])(GET|POST|PUT|DELETE|PATCH|HEAD|OPTIONS)(?![A-Za-z])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseRequest, Guid> _testCaseRequestRepository;
    private readonly IRepository<TestCaseExpectation, Guid> _testCaseExpectationRepository;
    private readonly IRepository<TestCaseVariable, Guid> _testCaseVariableRepository;
    private readonly IRepository<TestSuiteVersion, Guid> _versionRepository;
    private readonly IRepository<TestGenerationJob, Guid> _jobRepository;
    private readonly IRepository<TestCaseRequirementLink, Guid> _linkRepository;
    private readonly IRepository<SrsRequirement, Guid> _srsRequirementRepository;
    private readonly ILogger<SaveAiGeneratedTestCasesCommandHandler> _logger;

    public SaveAiGeneratedTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseRequest, Guid> testCaseRequestRepository,
        IRepository<TestCaseExpectation, Guid> testCaseExpectationRepository,
        IRepository<TestCaseVariable, Guid> testCaseVariableRepository,
        IRepository<TestSuiteVersion, Guid> versionRepository,
        IRepository<TestGenerationJob, Guid> jobRepository,
        IRepository<TestCaseRequirementLink, Guid> linkRepository,
        IRepository<SrsRequirement, Guid> srsRequirementRepository,
        ILogger<SaveAiGeneratedTestCasesCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _testCaseRequestRepository = testCaseRequestRepository;
        _testCaseExpectationRepository = testCaseExpectationRepository;
        _testCaseVariableRepository = testCaseVariableRepository;
        _versionRepository = versionRepository;
        _jobRepository = jobRepository;
        _linkRepository = linkRepository;
        _srsRequirementRepository = srsRequirementRepository;
        _logger = logger;
    }

    public async Task HandleAsync(SaveAiGeneratedTestCasesCommand command, CancellationToken cancellationToken = default)
    {
        if (command.TestSuiteId == Guid.Empty)
        {
            throw new ValidationException("TestSuiteId is required.");
        }

        if (command.TestCases == null || command.TestCases.Count == 0)
        {
            throw new ValidationException("At least one AI-generated test case is required.");
        }

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Test suite '{command.TestSuiteId}' was not found.");
        }

        if (suite.Status == TestSuiteStatus.Archived)
        {
            throw new ValidationException("Cannot save AI-generated test cases for an archived test suite.");
        }

        await _testCaseRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            // Build a set of valid SRS requirement IDs for this suite (pre-load to avoid FK violations).
            var validRequirementIds = new HashSet<Guid>();
            if (suite.SrsDocumentId.HasValue)
            {
                var requirements = await _srsRequirementRepository.ToListAsync(
                    _srsRequirementRepository.GetQueryableSet()
                        .Where(x => x.SrsDocumentId == suite.SrsDocumentId.Value)
                        .Select(x => x.Id));
                foreach (var id in requirements)
                {
                    validRequirementIds.Add(id);
                }
            }
            // Replace any previously AI-generated test cases for this suite.
            var existing = await _testCaseRepository.ToListAsync(
                _testCaseRepository.GetQueryableSet()
                    .Where(x => x.TestSuiteId == command.TestSuiteId));

            if (existing.Count > 0)
            {
                // Also delete any existing traceability links for these test cases.
                var existingIds = existing.Select(x => x.Id).ToHashSet();
                var existingLinks = await _linkRepository.ToListAsync(
                    _linkRepository.GetQueryableSet()
                        .Where(x => existingIds.Contains(x.TestCaseId)));
                if (existingLinks.Count > 0)
                {
                    await _linkRepository.BulkDeleteAsync(existingLinks, ct);
                }

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
                        HttpMethod = ResolveHttpMethod(dto),
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
                        HeaderChecks = NormalizeNullableJsonObject(dto.Expectation.HeaderChecks),
                        BodyContains = NormalizeNullableJsonArray(dto.Expectation.BodyContains),
                        BodyNotContains = NormalizeNullableJsonArray(dto.Expectation.BodyNotContains),
                        JsonPathChecks = NormalizeNullableJsonObject(dto.Expectation.JsonPathChecks),
                        MaxResponseTime = dto.Expectation.MaxResponseTime,
                        CreatedDateTime = now,
                    };
                    await _testCaseExpectationRepository.AddAsync(exp, ct);
                }

                // Persist variable extraction rules from LLM
                if (dto.Variables is { Count: > 0 })
                {
                    foreach (var v in dto.Variables)
                    {
                        if (string.IsNullOrWhiteSpace(v.VariableName) || string.IsNullOrWhiteSpace(v.ExtractFrom))
                        {
                            continue;
                        }

                        var variable = new TestCaseVariable
                        {
                            Id = Guid.NewGuid(),
                            TestCaseId = testCase.Id,
                            VariableName = v.VariableName,
                            ExtractFrom = ParseExtractFrom(v.ExtractFrom),
                            JsonPath = v.JsonPath,
                            HeaderName = v.HeaderName,
                            Regex = v.Regex,
                            DefaultValue = v.DefaultValue,
                        };
                        await _testCaseVariableRepository.AddAsync(variable, ct);
                    }
                }

                orderIdx++;
            }

            // Insert traceability links for test cases that reported coveredRequirementIds.
            // Validate all IDs against the suite's SRS document to prevent FK violations.
            var dtoList = command.TestCases;
            for (var i = 0; i < dtoList.Count && i < persistedTestCases.Count; i++)
            {
                var dto = dtoList[i];
                var tc = persistedTestCases[i];

                if (dto.CoveredRequirementIds == null || dto.CoveredRequirementIds.Count == 0)
                {
                    if (suite.SrsDocumentId.HasValue)
                    {
                        _logger.LogWarning(
                            "TestCase '{Name}' (index {Index}) generated without coveredRequirementIds but suite has SrsDocumentId={SrsDocumentId}. Traceability will be incomplete.",
                            tc.Name, i, suite.SrsDocumentId);
                    }

                    continue;
                }

                Guid? primaryReqId = null;
                foreach (var reqId in dto.CoveredRequirementIds.Distinct())
                {
                    if (reqId == Guid.Empty) continue;

                    if (validRequirementIds.Count > 0 && !validRequirementIds.Contains(reqId))
                    {
                        _logger.LogWarning(
                            "coveredRequirementId {ReqId} for TestCase '{Name}' does not belong to SrsDocumentId={SrsDocumentId}. Skipping link.",
                            reqId, tc.Name, suite.SrsDocumentId);
                        continue;
                    }

                    var link = new TestCaseRequirementLink
                    {
                        Id = Guid.NewGuid(),
                        TestCaseId = tc.Id,
                        SrsRequirementId = reqId,
                        TraceabilityScore = dto.TraceabilityScore ?? 1.0f,
                        MappingRationale = !string.IsNullOrWhiteSpace(dto.MappingRationale)
                            ? dto.MappingRationale
                            : "Auto-linked by LLM during test case generation.",
                    };
                    await _linkRepository.AddAsync(link, ct);

                    primaryReqId ??= reqId;
                }

                if (primaryReqId.HasValue && tc.PrimaryRequirementId == null)
                {
                    tc.PrimaryRequirementId = primaryReqId;
                    await _testCaseRepository.UpdateAsync(tc, ct);
                }
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

            // Update the latest generation job to Completed status
            var latestJob = await _jobRepository.FirstOrDefaultAsync(
                _jobRepository.GetQueryableSet()
                    .Where(x => x.TestSuiteId == command.TestSuiteId
                        && x.Status == GenerationJobStatus.WaitingForCallback)
                    .OrderByDescending(x => x.QueuedAt));

            if (latestJob != null)
            {
                latestJob.Status = GenerationJobStatus.Completed;
                latestJob.CompletedAt = now;
                latestJob.TestCasesGenerated = command.TestCases.Count;
                latestJob.RowVersion = Guid.NewGuid().ToByteArray();
                await _jobRepository.UpdateAsync(latestJob, ct);

                _logger.LogInformation(
                    "Updated generation job to Completed. JobId={JobId}, TestCasesGenerated={Count}",
                    latestJob.Id, command.TestCases.Count);
            }

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

    private static HttpMethod ResolveHttpMethod(AiGeneratedTestCaseDto dto)
    {
        if (TryParseHttpMethod(dto?.Request?.HttpMethod, out var method))
        {
            return method;
        }

        if (TryParseHttpMethod(dto?.Name, out method))
        {
            return method;
        }

        if (TryParseHttpMethod(dto?.Description, out method))
        {
            return method;
        }

        return HttpMethod.GET;
    }

    private static bool TryParseHttpMethod(string value, out HttpMethod method)
    {
        method = HttpMethod.GET;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (MapHttpMethod(value, out method))
        {
            return true;
        }

        var match = HttpMethodTokenRegex.Match(value);
        return match.Success && MapHttpMethod(match.Groups[1].Value, out method);
    }

    private static bool MapHttpMethod(string value, out HttpMethod method)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "GET":
                method = HttpMethod.GET;
                return true;
            case "POST":
                method = HttpMethod.POST;
                return true;
            case "PUT":
                method = HttpMethod.PUT;
                return true;
            case "DELETE":
                method = HttpMethod.DELETE;
                return true;
            case "PATCH":
                method = HttpMethod.PATCH;
                return true;
            case "HEAD":
                method = HttpMethod.HEAD;
                return true;
            case "OPTIONS":
                method = HttpMethod.OPTIONS;
                return true;
            default:
                method = HttpMethod.GET;
                return false;
        }
    }

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
            using var doc = JsonDocument.Parse(trimmed);
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
            using var doc = JsonDocument.Parse(trimmed);
            return trimmed;
        }
        catch
        {
            return JsonSerializer.Serialize(trimmed);
        }
    }

    /// <summary>Returns null when value is null/empty/whitespace or an empty JSON object <c>{}</c>.</summary>
    private static string NormalizeNullableJsonObject(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && !doc.RootElement.EnumerateObject().Any())
            {
                return null; // empty {} → treat as not set
            }

            return trimmed;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns null when value is null/empty/whitespace or an empty JSON array <c>[]</c>.</summary>
    private static string NormalizeNullableJsonArray(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() == 0)
            {
                return null; // empty [] → treat as not set
            }

            return trimmed;
        }
        catch
        {
            return null;
        }
    }

    private static ExtractFrom ParseExtractFrom(string extractFrom)
    {
        if (string.IsNullOrWhiteSpace(extractFrom))
        {
            return ExtractFrom.ResponseBody;
        }

        return extractFrom.Trim().ToLowerInvariant() switch
        {
            "responsebody" or "response_body" or "body" => ExtractFrom.ResponseBody,
            "requestbody" or "request_body" => ExtractFrom.RequestBody,
            "responseheader" or "response_header" or "header" => ExtractFrom.ResponseHeader,
            "status" => ExtractFrom.Status,
            _ => ExtractFrom.ResponseBody,
        };
    }
}
