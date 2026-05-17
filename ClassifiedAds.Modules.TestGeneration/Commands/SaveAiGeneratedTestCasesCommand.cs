using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

/// <summary>
/// Deserializes a field that may arrive as a JSON string <c>"[201]"</c>
/// or as a JSON integer/string array <c>[201]</c> or <c>["200","201"]</c>,
/// always converting to a canonical JSON string such as <c>"[201]"</c>.
/// </summary>
internal sealed class JsonArrayOrStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString();

            case JsonTokenType.Number:
                // single bare integer: 201 → "[201]"
                return $"[{reader.GetInt32()}]";

            case JsonTokenType.StartArray:
                var ints = new List<int>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.Number)
                    {
                        ints.Add(reader.GetInt32());
                    }
                    else if (reader.TokenType == JsonTokenType.String &&
                             int.TryParse(reader.GetString(), out var n))
                    {
                        ints.Add(n);
                    }
                }

                return ints.Count > 0
                    ? $"[{string.Join(",", ints)}]"
                    : "[200]";

            default:
                reader.Skip();
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

internal sealed class JsonStringOrRawJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}

/// <summary>Callback payload posted by n8n after LLM test-case generation.</summary>
public class AiTestCaseRequestDto
{
    public string HttpMethod { get; set; } = "GET";
    public string Url { get; set; }
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
    public string Headers { get; set; }
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
    public string PathParams { get; set; }
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
    public string QueryParams { get; set; }
    public string BodyType { get; set; } = "None";
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
    public string Body { get; set; }
    public int Timeout { get; set; } = 30000;
}

public class AiTestCaseExpectationDto
{
    [JsonConverter(typeof(JsonArrayOrStringConverter))]
    public string ExpectedStatus { get; set; } = "[200]";
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
    public string ResponseSchema { get; set; }
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
    public string HeaderChecks { get; set; }
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
    public string BodyContains { get; set; }
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
    public string BodyNotContains { get; set; }
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
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
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
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

    private static readonly Regex StatusCodeRegex = new(
        @"\b([1-5]\d\d)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex PlaceholderRegex = new(
        @"\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly ProposalStatus[] ActiveProposalStatuses =
    {
        ProposalStatus.Approved,
        ProposalStatus.ModifiedAndApproved,
        ProposalStatus.Applied,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseDependency, Guid> _testCaseDependencyRepository;
    private readonly IRepository<TestCaseRequest, Guid> _testCaseRequestRepository;
    private readonly IRepository<TestCaseExpectation, Guid> _testCaseExpectationRepository;
    private readonly IRepository<TestCaseVariable, Guid> _testCaseVariableRepository;
    private readonly IRepository<TestSuiteVersion, Guid> _versionRepository;
    private readonly IRepository<TestGenerationJob, Guid> _jobRepository;
    private readonly IRepository<TestCaseRequirementLink, Guid> _linkRepository;
    private readonly IRepository<SrsRequirement, Guid> _srsRequirementRepository;
    private readonly IRepository<TestOrderProposal, Guid> _proposalRepository;
    private readonly IApiTestOrderService _apiTestOrderService;
    private readonly IApiEndpointMetadataService _apiEndpointMetadataService;
    private readonly IEndpointRequirementMapper _requirementMapper;
    private readonly ILogger<SaveAiGeneratedTestCasesCommandHandler> _logger;

    public SaveAiGeneratedTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseDependency, Guid> testCaseDependencyRepository,
        IRepository<TestCaseRequest, Guid> testCaseRequestRepository,
        IRepository<TestCaseExpectation, Guid> testCaseExpectationRepository,
        IRepository<TestCaseVariable, Guid> testCaseVariableRepository,
        IRepository<TestSuiteVersion, Guid> versionRepository,
        IRepository<TestGenerationJob, Guid> jobRepository,
        IRepository<TestCaseRequirementLink, Guid> linkRepository,
        IRepository<SrsRequirement, Guid> srsRequirementRepository,
        IRepository<TestOrderProposal, Guid> proposalRepository,
        IApiTestOrderService apiTestOrderService,
        IApiEndpointMetadataService apiEndpointMetadataService,
        IEndpointRequirementMapper requirementMapper,
        ILogger<SaveAiGeneratedTestCasesCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _testCaseRepository = testCaseRepository;
        _testCaseDependencyRepository = testCaseDependencyRepository;
        _testCaseRequestRepository = testCaseRequestRepository;
        _testCaseExpectationRepository = testCaseExpectationRepository;
        _testCaseVariableRepository = testCaseVariableRepository;
        _versionRepository = versionRepository;
        _jobRepository = jobRepository;
        _linkRepository = linkRepository;
        _srsRequirementRepository = srsRequirementRepository;
        _proposalRepository = proposalRepository;
        _apiTestOrderService = apiTestOrderService;
        _apiEndpointMetadataService = apiEndpointMetadataService;
        _requirementMapper = requirementMapper;
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
            var requirementsById = new Dictionary<Guid, SrsRequirement>();
            if (suite.SrsDocumentId.HasValue)
            {
                var requirements = await _srsRequirementRepository.ToListAsync(
                    _srsRequirementRepository.GetQueryableSet()
                        .Where(x => x.SrsDocumentId == suite.SrsDocumentId.Value));

                requirementsById = requirements
                    .GroupBy(x => x.Id)
                    .Select(x => x.First())
                    .ToDictionary(x => x.Id);

                var endpointRequirementMap = await BuildEndpointRequirementMapAsync(
                    suite,
                    command.TestCases,
                    requirementsById,
                    ct);

                ValidateGeneratedTestCasesAgainstSrs(
                    command.TestCases,
                    requirementsById,
                    endpointRequirementMap);
            }

            await ValidateGeneratedTestCasesAgainstOpenApiContractAsync(
                suite,
                command.TestCases,
                ct);

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
                    Tags = EnsureLlmSourcedTagJson(dto.Tags, ParseTestType(dto.TestType)),
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
                    testCase.Request = req;
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
                    testCase.Expectation = exp;
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
                        testCase.Variables.Add(variable);
                    }
                }

                orderIdx++;
            }

            var approvedOrder = await LoadApprovedOrderAsync(command.TestSuiteId, ct);
            if (approvedOrder.Count > 0)
            {
                var enrichment = GeneratedTestCaseDependencyEnricher.Enrich(persistedTestCases, approvedOrder);

                foreach (var dependency in persistedTestCases.SelectMany(x => x.Dependencies))
                {
                    dependency.CreatedDateTime = now;
                    await _testCaseDependencyRepository.AddAsync(dependency, ct);
                }

                foreach (var producerVariable in enrichment.ExistingProducerVariablesToPersist)
                {
                    producerVariable.CreatedDateTime = now;
                    await _testCaseVariableRepository.AddAsync(producerVariable, ct);
                }
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

                    if (requirementsById.Count > 0 && !requirementsById.ContainsKey(reqId))
                    {
                        throw new ValidationException(
                            $"AI-generated test case '{tc.Name}' references coveredRequirementId '{reqId}', but it does not belong to SRS document '{suite.SrsDocumentId}'.");
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

    /// <summary>
    /// Ensures the saved test case always carries the "llm-suggested" and "auto-generated" tags
    /// so that <see cref="VariableResolver.IsLlmSourced"/> returns <c>true</c> and the
    /// body-normalisation pipeline is skipped at execution time.
    /// </summary>
    private static string EnsureLlmSourcedTagJson(string existingTagsJson, TestType testType)
    {
        var tags = new List<string>();

        // Deserialise whatever n8n sent (may be null/empty/plain string/JSON array).
        var normalised = NormalizeTagsJson(existingTagsJson);
        try
        {
            var parsed = JsonSerializer.Deserialize<List<string>>(normalised, JsonOpts);
            if (parsed != null)
            {
                tags.AddRange(parsed);
            }
        }
        catch { }

        // Add canonical type tag.
        var typeTag = testType switch
        {
            TestType.HappyPath  => "happy-path",
            TestType.Boundary   => "boundary",
            TestType.Negative   => "negative",
            TestType.Security   => "security",
            TestType.Performance => "performance",
            _                   => "negative",
        };
        if (!tags.Contains(typeTag, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(typeTag);
        }

        // Ensure markers required by IsLlmSourced.
        foreach (var required in new[] { "auto-generated", "llm-suggested" })
        {
            if (!tags.Contains(required, StringComparer.OrdinalIgnoreCase))
            {
                tags.Add(required);
            }
        }

        return JsonSerializer.Serialize(tags, JsonOpts);
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

    private async Task<Dictionary<Guid, HashSet<Guid>>> BuildEndpointRequirementMapAsync(
        TestSuite suite,
        IReadOnlyList<AiGeneratedTestCaseDto> testCases,
        IReadOnlyDictionary<Guid, SrsRequirement> requirementsById,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, HashSet<Guid>>();
        if (suite?.ApiSpecId is not Guid specificationId ||
            specificationId == Guid.Empty ||
            testCases == null ||
            testCases.Count == 0 ||
            requirementsById == null ||
            requirementsById.Count == 0)
        {
            return result;
        }

        var endpointIds = testCases
            .Select(x => x?.EndpointId)
            .Where(x => x.HasValue && x.Value != Guid.Empty)
            .Select(x => x.Value)
            .Distinct()
            .ToList();

        if (endpointIds.Count == 0)
        {
            return result;
        }

        var endpoints = await _apiEndpointMetadataService.GetEndpointMetadataAsync(
            specificationId,
            endpointIds,
            cancellationToken);

        foreach (var endpoint in endpoints ?? Array.Empty<Contracts.ApiDocumentation.DTOs.ApiEndpointMetadataDto>())
        {
            var coverableRequirementIds = _requirementMapper
                .MapRequirementsToEndpoint(endpoint, requirementsById.Values.ToList())
                .Where(x => x.IsCoverable)
                .Select(x => x.Requirement?.Id ?? Guid.Empty)
                .Where(x => x != Guid.Empty)
                .ToHashSet();

            result[endpoint.EndpointId] = coverableRequirementIds;
        }

        return result;
    }

    private async Task<IReadOnlyList<ApiOrderItemModel>> LoadApprovedOrderAsync(
        Guid testSuiteId,
        CancellationToken cancellationToken)
    {
        var proposal = await _proposalRepository.FirstOrDefaultAsync(
            _proposalRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == testSuiteId
                    && ActiveProposalStatuses.Contains(x.Status)
                    && x.AppliedOrder != null)
                .OrderByDescending(x => x.ProposalNumber));

        if (proposal == null)
        {
            return Array.Empty<ApiOrderItemModel>();
        }

        return _apiTestOrderService.DeserializeOrderJson(proposal.AppliedOrder) ??
               Array.Empty<ApiOrderItemModel>();
    }

    private static void ValidateGeneratedTestCasesAgainstSrs(
        IReadOnlyList<AiGeneratedTestCaseDto> testCases,
        IReadOnlyDictionary<Guid, SrsRequirement> requirementsById,
        IReadOnlyDictionary<Guid, HashSet<Guid>> endpointRequirementMap)
    {
        if (testCases == null || testCases.Count == 0)
        {
            return;
        }

        for (var i = 0; i < testCases.Count; i++)
        {
            var dto = testCases[i];
            if (dto?.CoveredRequirementIds == null || dto.CoveredRequirementIds.Count == 0)
            {
                continue;
            }

            var validRequirementIds = new List<Guid>();
            foreach (var reqId in dto.CoveredRequirementIds.Where(x => x != Guid.Empty).Distinct())
            {
                if (!requirementsById.TryGetValue(reqId, out var requirement))
                {
                    throw new ValidationException(
                        $"AI-generated test case '{dto.Name ?? $"index {i}"}' references coveredRequirementId '{reqId}', but that requirement does not belong to the suite SRS document.");
                }

                if (dto.EndpointId.HasValue &&
                    endpointRequirementMap != null &&
                    endpointRequirementMap.TryGetValue(dto.EndpointId.Value, out var relevantIds) &&
                    !relevantIds.Contains(reqId))
                {
                    continue;
                }

                validRequirementIds.Add(reqId);
            }

            dto.CoveredRequirementIds = validRequirementIds;
        }
    }

    private static List<int> ParseStatusCodesFromExpectation(string expectedStatus)
    {
        if (string.IsNullOrWhiteSpace(expectedStatus))
        {
            return new List<int> { 200 };
        }

        var trimmed = expectedStatus.Trim();
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return ParseStatusCodesFromJsonElement(doc.RootElement);
        }
        catch
        {
            return ParseStatusCodesFromText(trimmed);
        }
    }

    private static List<int> ExtractExplicitStatusCodes(SrsRequirement requirement)
    {
        var raw = !string.IsNullOrWhiteSpace(requirement?.RefinedConstraints)
            ? requirement.RefinedConstraints
            : requirement?.TestableConstraints;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<int>();
        }

        var statuses = new List<int>();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        statuses.AddRange(ParseStatusCodesFromJsonElement(item));
                        continue;
                    }

                    foreach (var propertyName in new[] { "expectedOutcome", "constraint" })
                    {
                        if (item.TryGetProperty(propertyName, out var property))
                        {
                            statuses.AddRange(ParseStatusCodesFromJsonElement(property));
                        }
                    }
                }
            }
            else if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var propertyName in new[] { "expectedOutcome", "constraint" })
                {
                    if (doc.RootElement.TryGetProperty(propertyName, out var property))
                    {
                        statuses.AddRange(ParseStatusCodesFromJsonElement(property));
                    }
                }
            }
        }
        catch
        {
            statuses.AddRange(ParseStatusCodesFromText(raw));
        }

        return statuses
            .Where(code => code >= 100 && code <= 599)
            .Distinct()
            .OrderBy(code => code)
            .ToList();
    }

    private static List<int> ParseStatusCodesFromJsonElement(JsonElement element)
    {
        var statuses = new List<int>();
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var code))
                {
                    statuses.Add(code);
                }
                break;
            case JsonValueKind.String:
                statuses.AddRange(ParseStatusCodesFromText(element.GetString()));
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                {
                    statuses.AddRange(ParseStatusCodesFromJsonElement(child));
                }
                break;
        }

        return statuses
            .Where(code => code >= 100 && code <= 599)
            .Distinct()
            .OrderBy(code => code)
            .ToList();
    }

    private static List<int> ParseStatusCodesFromText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<int>();
        }

        return StatusCodeRegex.Matches(value)
            .Select(x => int.TryParse(x.Groups[1].Value, out var code) ? code : 0)
            .Where(code => code >= 100 && code <= 599)
            .Distinct()
            .OrderBy(code => code)
            .ToList();
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

    private async Task ValidateGeneratedTestCasesAgainstOpenApiContractAsync(
        TestSuite suite,
        IReadOnlyList<AiGeneratedTestCaseDto> testCases,
        CancellationToken cancellationToken)
    {
        if (suite?.ApiSpecId is not Guid specId || specId == Guid.Empty || testCases == null || testCases.Count == 0)
        {
            return;
        }

        var endpointIds = testCases
            .Where(x => x?.EndpointId is Guid id && id != Guid.Empty)
            .Select(x => x.EndpointId!.Value)
            .Distinct()
            .ToList();

        if (endpointIds.Count == 0)
        {
            return;
        }

        var metadata = await _apiEndpointMetadataService.GetEndpointMetadataAsync(specId, endpointIds, cancellationToken);
        var metadataByEndpointId = metadata
            .GroupBy(x => x.EndpointId)
            .Select(x => x.First())
            .ToDictionary(x => x.EndpointId);

        var ordered = testCases
            .Select((x, i) => new { TestCase = x, Index = i })
            .OrderBy(x => x.TestCase?.OrderIndex ?? x.Index)
            .ThenBy(x => x.Index)
            .ToList();

        var producedVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tcUniqueId",
        };

        foreach (var item in ordered)
        {
            var dto = item.TestCase;
            if (dto == null || dto.EndpointId is not Guid endpointId || endpointId == Guid.Empty)
            {
                continue;
            }

            if (!metadataByEndpointId.TryGetValue(endpointId, out var endpoint))
            {
                throw new ValidationException(
                    $"AI-generated test case '{dto.Name ?? $"index {item.Index}"}' references unknown endpointId '{endpointId}' for specification '{specId}'.");
            }

            ValidateExpectedStatusesAgainstOpenApi(dto, endpoint, item.Index);
            ValidateAuthorizationHeaderAgainstOpenApi(dto, endpoint, item.Index);
            ValidateVariableDependencies(dto, producedVariables, item.Index);
            RegisterProducedVariables(dto, producedVariables);
        }
    }

    private static void ValidateExpectedStatusesAgainstOpenApi(
        AiGeneratedTestCaseDto dto,
        ApiEndpointMetadataDto endpoint,
        int index)
    {
        var expectedStatuses = ParseStatusCodesFromExpectation(dto?.Expectation?.ExpectedStatus);
        if (expectedStatuses.Count == 0)
        {
            throw new ValidationException(
                $"AI-generated test case '{dto?.Name ?? $"index {index}"}' is missing expectation.expectedStatus.");
        }

        var allowedStatuses = endpoint?.Responses?
            .Select(x => x.StatusCode)
            .Where(code => code >= 100 && code <= 599)
            .Distinct()
            .ToHashSet() ?? new HashSet<int>();

        if (allowedStatuses.Count == 0)
        {
            return;
        }

        var invalid = expectedStatuses.Where(x => !allowedStatuses.Contains(x)).Distinct().ToList();
        if (invalid.Count > 0)
        {
            throw new ValidationException(
                $"AI-generated test case '{dto?.Name ?? $"index {index}"}' has expectedStatus [{string.Join(", ", expectedStatuses)}] " +
                $"but OpenAPI for endpoint '{endpoint.HttpMethod} {endpoint.Path}' allows [{string.Join(", ", allowedStatuses.OrderBy(x => x))}].");
        }
    }

    private static void ValidateAuthorizationHeaderAgainstOpenApi(
        AiGeneratedTestCaseDto dto,
        ApiEndpointMetadataDto endpoint,
        int index)
    {
        if (endpoint?.IsAuthRelated == true)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(dto?.Request?.Headers))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(dto.Request.Headers);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ValidationException(
                        $"AI-generated test case '{dto?.Name ?? $"index {index}"}' adds Authorization header, " +
                        $"but endpoint '{endpoint.HttpMethod} {endpoint.Path}' is public in OpenAPI.");
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed header JSON here; persistence normalization will handle invalid shapes.
        }
    }

    private static void ValidateVariableDependencies(
        AiGeneratedTestCaseDto dto,
        HashSet<string> producedVariables,
        int index)
    {
        var requiredVariables = ExtractPlaceholders(dto);
        if (requiredVariables.Count == 0)
        {
            return;
        }

        var locallyDefined = dto?.Variables?
            .Where(x => !string.IsNullOrWhiteSpace(x?.VariableName))
            .Select(x => x.VariableName.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var variable in requiredVariables)
        {
            if (locallyDefined.Contains(variable) || producedVariables.Contains(variable))
            {
                continue;
            }

            throw new ValidationException(
                $"AI-generated test case '{dto?.Name ?? $"index {index}"}' uses placeholder '{{{{{variable}}}}}' " +
                "but no producer/extractor exists in previous test cases or this test case variables.");
        }
    }

    private static HashSet<string> ExtractPlaceholders(AiGeneratedTestCaseDto dto)
    {
        var values = new[]
        {
            dto?.Request?.Url,
            dto?.Request?.Headers,
            dto?.Request?.PathParams,
            dto?.Request?.QueryParams,
            dto?.Request?.Body,
            dto?.Expectation?.BodyContains,
            dto?.Expectation?.BodyNotContains,
            dto?.Expectation?.HeaderChecks,
            dto?.Expectation?.JsonPathChecks,
        };

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (Match match in PlaceholderRegex.Matches(value))
            {
                var name = match.Groups[1].Value?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                result.Add(name);
            }
        }

        return result;
    }

    private static void RegisterProducedVariables(
        AiGeneratedTestCaseDto dto,
        HashSet<string> producedVariables)
    {
        if (dto?.Variables == null || dto.Variables.Count == 0)
        {
            return;
        }

        foreach (var variable in dto.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable?.VariableName))
            {
                continue;
            }

            producedVariables.Add(variable.VariableName.Trim());
        }
    }
}
