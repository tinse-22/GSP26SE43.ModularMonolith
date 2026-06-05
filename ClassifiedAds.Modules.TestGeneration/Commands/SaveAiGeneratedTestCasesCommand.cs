using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.TestGeneration.Validation;
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
    public string ExpectationSource { get; set; }
    public string RequirementCode { get; set; }
    public Guid? PrimaryRequirementId { get; set; }
    [JsonConverter(typeof(JsonStringOrRawJsonConverter))]
    public string ExpectedProvenance { get; set; }
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
    private string _name;

    public string ScenarioKey { get; set; }

    public Guid? EndpointId { get; set; }
    public string Name
    {
        get => !string.IsNullOrWhiteSpace(_name) ? _name : ScenarioName;
        set => _name = value;
    }

    /// <summary>Legacy n8n scenario shape uses scenarioName instead of name.</summary>
    public string ScenarioName { get; set; }

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

    public N8nExecutionHints ExecutionHints { get; set; }
    public string AuthMode { get; set; }
    public string CredentialPolicy { get; set; }
    public List<string> LockedFields { get; set; } = new();
    public bool? FlowRequired { get; set; }
    public string FlowId { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public List<string> Produces { get; set; } = new();
    public List<string> Consumes { get; set; } = new();

    /// <summary>
    /// SRS requirement IDs that this test case covers.
    /// Populated by the LLM when the generation payload includes SRS requirements context.
    /// </summary>
    public List<Guid> CoveredRequirementIds { get; set; } = new();

    /// <summary>
    /// Legacy n8n/SRS requirement codes. Mapped to <see cref="CoveredRequirementIds"/> before validation.
    /// </summary>
    public List<string> CoveredRequirementCodes { get; set; } = new();

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
    private static readonly Regex SingleBraceTokenRegex = new(
        @"(?<!\{)\{[A-Za-z_][A-Za-z0-9_]*\}(?!\})",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private const string RewritePolicyTagPrefix = "rewrite-policy:";
    private const string AuthFallbackTagPrefix = "auth-fallback:";
    private const string AuthModeTagPrefix = "auth-mode:";
    private const string CredentialPolicyTagPrefix = "cred-policy:";
    private const string CredentialLockTagPrefix = "cred-lock:";
    private const string FlowScenarioKeyTagPrefix = "flow-scenario-key:";
    private const string FlowRequiredTagPrefix = "flow-required:";
    private const string FlowDependsOnTagPrefix = "flow-depends-on:";
    private const string FlowProducesTagPrefix = "flow-produces:";
    private const string FlowConsumesTagPrefix = "flow-consumes:";

    private static readonly HashSet<string> BuiltInRuntimeVariableNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "tcUniqueId",
        "timestamp",
        "randomInt",
        "uuid",
        "runId",
        "runSuffix",
        "runIdSuffix",
        "runTimestamp",
    };

    private static readonly string[] AuthModeHeaderNames =
    {
        "X-Test-Auth-Mode",
        "X-Auth-Mode",
        "X-LLM-Auth-Mode",
    };

    private static readonly Regex JavaScriptRepeatExpressionRegex = new(
        @"\.repeat\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

        var validTestCases = await SanitizeAndFilterTestCasesAsync(
            suite,
            command.TestCases,
            cancellationToken);

        if (validTestCases.Count == 0)
        {
            throw new ValidationException(
                "All AI-generated test cases were dropped because request.httpMethod/request.url could not be resolved from endpointId.");
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
            }

            MapCoveredRequirementCodesToIds(command.TestCases, requirementsById);

            var filteredTestCases = await ApplyGeneratedTestCaseQualityFilterAsync(
                suite,
                command.TestCases,
                ct);
            command.TestCases = filteredTestCases.ToList();
            validTestCases = command.TestCases;
            var metadataByEndpointIdForPersistence = await LoadEndpointMetadataForQualityFilterAsync(
                suite,
                validTestCases,
                ct);

            if (command.TestCases.Count == 0)
            {
                throw new ValidationException("All AI-generated test cases were filtered out by backend quality checks.");
            }

            if (suite.SrsDocumentId.HasValue)
            {
                var endpointRequirementMap = await BuildEndpointRequirementMapAsync(
                    suite,
                    validTestCases,
                    requirementsById,
                    ct);

                ValidateGeneratedTestCasesAgainstSrs(
                    validTestCases,
                    requirementsById,
                    endpointRequirementMap);
            }

            await ValidateGeneratedTestCasesAgainstOpenApiContractAsync(
                suite,
                validTestCases,
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
            var persistedTestCases = new List<TestCase>(validTestCases.Count);
            var approvedOrder = await LoadApprovedOrderAsync(command.TestSuiteId, ct);
            var orderItemByEndpointId = approvedOrder
                .GroupBy(x => x.EndpointId)
                .ToDictionary(x => x.Key, x => x.First());
            var orderIdx = 0;
            foreach (var dto in validTestCases)
            {
                metadataByEndpointIdForPersistence.TryGetValue(dto.EndpointId ?? Guid.Empty, out var endpointMetadata);
                orderItemByEndpointId.TryGetValue(dto.EndpointId ?? Guid.Empty, out var orderItem);
                NormalizeRequestBodyForEndpoint(dto, endpointMetadata);
                ApplyRuntimeAuthHints(dto, endpointMetadata, orderItem, metadataByEndpointIdForPersistence, orderItemByEndpointId);

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
                    Tags = EnsureLlmSourcedTagJson(dto.Tags, ParseTestType(dto.TestType), dto),
                    Version = 1,
                    LastModifiedById = actorUserId,
                    CreatedDateTime = now,
                };

                await _testCaseRepository.AddAsync(testCase, ct);
                persistedTestCases.Add(testCase);

                if (dto.Request is not null)
                {
                    metadataByEndpointIdForPersistence.TryGetValue(dto.EndpointId ?? Guid.Empty, out var endpointMetadata);
                    NormalizeRequestBodyForEndpoint(dto, endpointMetadata);

                    var req = new TestCaseRequest
                    {
                        Id = Guid.NewGuid(),
                        TestCaseId = testCase.Id,
                        HttpMethod = ResolveHttpMethod(dto),
                        Url = dto.Request.Url,
                        Headers = NormalizeSanitizedHeaders(dto),
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
                    var primaryRequirement = ResolvePrimaryRequirement(dto, requirementsById);
                    var expectedProvenance = ExpectedProvenanceBuilder.BuildFromSerializedExpectation(
                        dto.Expectation.ExpectedStatus,
                        dto.Expectation.BodyContains,
                        dto.Expectation.BodyNotContains,
                        dto.Expectation.HeaderChecks,
                        dto.Expectation.JsonPathChecks,
                        dto.Expectation.MaxResponseTime,
                        dto.Expectation.ExpectedProvenance,
                        dto.Expectation.ExpectationSource ?? (primaryRequirement != null ? "Srs" : "Llm"),
                        dto.Expectation.RequirementCode ?? primaryRequirement?.RequirementCode,
                        primaryRequirement);
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
                        ExpectationSource = dto.Expectation.ExpectationSource ?? (primaryRequirement != null ? ExpectationSource.Srs.ToString() : ExpectationSource.Llm.ToString()),
                        RequirementCode = dto.Expectation.RequirementCode ?? primaryRequirement?.RequirementCode,
                        PrimaryRequirementId = dto.Expectation.PrimaryRequirementId ?? primaryRequirement?.Id,
                        ExpectedProvenance = expectedProvenance,
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
                        if (string.IsNullOrWhiteSpace(v.VariableName))
                        {
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(v.JsonPath) &&
                            string.IsNullOrWhiteSpace(v.HeaderName) &&
                            string.IsNullOrWhiteSpace(v.Regex) &&
                            string.IsNullOrWhiteSpace(v.DefaultValue))
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

                await EnsureRequestBodyProducerAliasVariablesAsync(dto, testCase, now, ct);

                orderIdx++;
            }

            GeneratedTestCaseEnrichmentResult enrichment = null;
            if (approvedOrder.Count > 0)
            {
                enrichment = GeneratedTestCaseDependencyEnricher.Enrich(persistedTestCases, approvedOrder);
            }

            ApplyExplicitFlowDependenciesFromTags(persistedTestCases, Array.Empty<TestCase>());

            foreach (var dependency in persistedTestCases.SelectMany(x => x.Dependencies))
            {
                dependency.CreatedDateTime = now;
                await _testCaseDependencyRepository.AddAsync(dependency, ct);
            }

            if (enrichment != null)
            {
                foreach (var producerVariable in enrichment.ExistingProducerVariablesToPersist)
                {
                    producerVariable.CreatedDateTime = now;
                    await _testCaseVariableRepository.AddAsync(producerVariable, ct);
                }
            }

            // Insert traceability links for test cases that reported coveredRequirementIds.
            // Validate all IDs against the suite's SRS document to prevent FK violations.
            var dtoList = validTestCases;
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
                latestJob.TestCasesGenerated = validTestCases.Count;
                latestJob.RowVersion = Guid.NewGuid().ToByteArray();
                await _jobRepository.UpdateAsync(latestJob, ct);

                _logger.LogInformation(
                    "Updated generation job to Completed. JobId={JobId}, TestCasesGenerated={Count}",
                    latestJob.Id, validTestCases.Count);
            }

            await _testCaseRepository.UnitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Saved {Count} AI-generated test cases and marked suite Ready for TestSuiteId={TestSuiteId}",
                validTestCases.Count,
                command.TestSuiteId);
        }, cancellationToken: cancellationToken);
    }

    private async Task<List<AiGeneratedTestCaseDto>> SanitizeAndFilterTestCasesAsync(
        TestSuite suite,
        IReadOnlyList<AiGeneratedTestCaseDto> testCases,
        CancellationToken cancellationToken)
    {
        if (testCases == null || testCases.Count == 0)
        {
            return new List<AiGeneratedTestCaseDto>();
        }

        var metadataByEndpointId = new Dictionary<Guid, ApiEndpointMetadataDto>();
        var specId = suite?.ApiSpecId ?? Guid.Empty;
        var requiresMetadata = specId != Guid.Empty;
        if (requiresMetadata)
        {
            var endpointIds = testCases
                .Where(x => x?.EndpointId is Guid id && id != Guid.Empty)
                .Select(x => x.EndpointId!.Value)
                .Distinct()
                .ToList();

            if (endpointIds.Count > 0)
            {
                var metadata = await _apiEndpointMetadataService.GetEndpointMetadataAsync(specId, endpointIds, cancellationToken);
                metadataByEndpointId = metadata
                    .GroupBy(x => x.EndpointId)
                    .Select(x => x.First())
                    .ToDictionary(x => x.EndpointId);
            }
        }

        var valid = new List<AiGeneratedTestCaseDto>(testCases.Count);
        for (var i = 0; i < testCases.Count; i++)
        {
            var dto = testCases[i];
            if (dto == null || dto.EndpointId is not Guid endpointId || endpointId == Guid.Empty)
            {
                _logger.LogWarning("Drop AI test case at index {Index}: missing endpointId.", i);
                continue;
            }

            dto.Request ??= new AiTestCaseRequestDto();

            if (!requiresMetadata)
            {
                if (!HasResolvableHttpMethod(dto) || string.IsNullOrWhiteSpace(dto.Request.Url))
                {
                    _logger.LogWarning(
                        "Drop AI test case '{Name}' (index {Index}): unresolved request shape without API metadata. endpointId={EndpointId}, method='{Method}', url='{Url}'.",
                        dto.Name ?? $"index {i}",
                        i,
                        endpointId,
                        dto.Request.HttpMethod,
                        dto.Request.Url);
                    continue;
                }

                valid.Add(dto);
                continue;
            }

            if (!metadataByEndpointId.TryGetValue(endpointId, out var endpoint))
            {
                _logger.LogWarning("Drop AI test case '{Name}' (index {Index}): endpointId {EndpointId} not found in metadata.", dto.Name ?? $"index {i}", i, endpointId);
                continue;
            }

            if (!TryParseHttpMethod(dto.Request.HttpMethod, out _) && !string.IsNullOrWhiteSpace(endpoint.HttpMethod))
            {
                dto.Request.HttpMethod = endpoint.HttpMethod.Trim().ToUpperInvariant();
            }

            if (string.IsNullOrWhiteSpace(dto.Request.Url) && !string.IsNullOrWhiteSpace(endpoint.Path))
            {
                dto.Request.Url = endpoint.Path.Trim();
            }

            if (!TryParseHttpMethod(dto.Request.HttpMethod, out _) || string.IsNullOrWhiteSpace(dto.Request.Url))
            {
                _logger.LogWarning(
                    "Drop AI test case '{Name}' (index {Index}): unresolved request shape after auto-fill. endpointId={EndpointId}, method='{Method}', url='{Url}'.",
                    dto.Name ?? $"index {i}",
                    i,
                    endpointId,
                    dto.Request.HttpMethod,
                    dto.Request.Url);
                continue;
            }

            endpoint = await ResolveEndpointMetadataForRequestAsync(
                specId,
                dto,
                endpoint,
                cancellationToken);
            endpointId = dto.EndpointId!.Value;

            NormalizeRequestBodyForEndpoint(dto, endpoint);
            valid.Add(dto);
        }

        if (valid.Count != testCases.Count)
        {
            _logger.LogWarning(
                "Sanitized AI test cases. Input={InputCount}, Valid={ValidCount}, Dropped={DroppedCount}.",
                testCases.Count,
                valid.Count,
                testCases.Count - valid.Count);
        }

        return valid;
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

    private static bool HasResolvableHttpMethod(AiGeneratedTestCaseDto dto)
        => TryParseHttpMethod(dto?.Request?.HttpMethod, out _)
            || TryParseHttpMethod(dto?.Name, out _)
            || TryParseHttpMethod(dto?.Description, out _);

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

    private static void NormalizeRequestBodyForEndpoint(
        AiGeneratedTestCaseDto dto,
        ApiEndpointMetadataDto endpoint)
    {
        if (dto?.Request == null)
        {
            return;
        }

        var methodParsed = TryParseHttpMethod(dto.Request.HttpMethod, out var method);
        var hasBody = !string.IsNullOrWhiteSpace(dto.Request.Body);
        var bodyType = ParseBodyType(dto.Request.BodyType);

        if (methodParsed && MethodForbidsRequestBody(method))
        {
            if (hasBody || bodyType != BodyType.None)
            {
                dto.Request.Body = null;
                dto.Request.BodyType = "None";
            }

            return;
        }

        if (!hasBody)
        {
            if (bodyType != BodyType.None)
            {
                dto.Request.BodyType = "None";
            }

            return;
        }

        if (methodParsed
            && bodyType == BodyType.None
            && MethodCanCarryGeneratedBody(method)
            && LooksLikeJsonBody(dto.Request.Body))
        {
            dto.Request.BodyType = "JSON";
        }
    }

    private static void ApplyRuntimeAuthHints(
        AiGeneratedTestCaseDto dto,
        ApiEndpointMetadataDto endpoint,
        ApiOrderItemModel orderItem,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataByEndpointId,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemByEndpointId)
    {
        if (dto?.Request == null ||
            !EndpointDependsOnAuthBootstrap(endpoint, orderItem, metadataByEndpointId, orderItemByEndpointId))
        {
            return;
        }

        var tags = ParseTagList(dto.Tags);
        if (IsNoAuthIntent(dto, tags) || HasConcreteAuthHeader(dto.Request.Headers))
        {
            return;
        }

        dto.ExecutionHints ??= new N8nExecutionHints();
        if (string.IsNullOrWhiteSpace(dto.ExecutionHints.AuthMode) &&
            string.IsNullOrWhiteSpace(dto.AuthMode) &&
            string.IsNullOrWhiteSpace(ReadAuthModeHeader(dto.Request.Headers)))
        {
            dto.ExecutionHints.AuthMode = "required";
        }

        dto.ExecutionHints.Consumes ??= new List<string>();
        if (!dto.ExecutionHints.Consumes.Any(IsTokenLikeVariableName) &&
            !NormalizeStringList(dto.Consumes).Any(IsTokenLikeVariableName))
        {
            dto.ExecutionHints.Consumes.Add("authToken");
        }
    }

    private static bool EndpointDependsOnAuthBootstrap(
        ApiEndpointMetadataDto endpoint,
        ApiOrderItemModel orderItem,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataByEndpointId,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemByEndpointId)
    {
        if (endpoint?.IsAuthRelated == true || orderItem?.IsAuthRelated == true)
        {
            return false;
        }

        return HasAuthBootstrapDependency(endpoint?.DependsOnEndpointIds, metadataByEndpointId, orderItemByEndpointId) ||
               HasAuthBootstrapDependency(orderItem?.DependsOnEndpointIds, metadataByEndpointId, orderItemByEndpointId);
    }

    private static bool HasAuthBootstrapDependency(
        IEnumerable<Guid> dependencyIds,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataByEndpointId,
        IReadOnlyDictionary<Guid, ApiOrderItemModel> orderItemByEndpointId)
    {
        if (dependencyIds == null)
        {
            return false;
        }

        foreach (var dependencyId in dependencyIds)
        {
            if (metadataByEndpointId?.TryGetValue(dependencyId, out var dependencyMetadata) == true &&
                dependencyMetadata.IsAuthRelated)
            {
                return true;
            }

            if (orderItemByEndpointId?.TryGetValue(dependencyId, out var dependencyOrderItem) == true &&
                dependencyOrderItem.IsAuthRelated)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasConcreteAuthHeader(string headersJson)
    {
        if (!TryReadJsonObject(headersJson, out var headers))
        {
            return false;
        }

        return headers.Any(x =>
            IsAuthHeaderName(x.Key) &&
            !string.IsNullOrWhiteSpace(x.Value) &&
            !PlaceholderRegex.IsMatch(x.Value));
    }

    private static bool EndpointDeclaresRequestBody(ApiEndpointMetadataDto endpoint)
    {
        if (endpoint == null)
        {
            return false;
        }

        if (endpoint.HasRequiredRequestBody)
        {
            return true;
        }

        if (endpoint.Parameters?.Any(IsRequestBodyParameter) == true)
        {
            return true;
        }

        return endpoint.ParameterSchemaPayloads?.Any(LooksLikeWholeRequestBodySchema) == true;
    }

    private static bool IsRequestBodyParameter(ApiEndpointParameterDescriptorDto parameter)
    {
        if (parameter == null)
        {
            return false;
        }

        var location = parameter.Location?.Trim().ToLowerInvariant();
        if (location is "body" or "requestbody" or "request_body" or "formdata" or "form-data")
        {
            return true;
        }

        if (location is "path" or "query" or "header" or "cookie")
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(parameter.ContentType)
            || LooksLikeWholeRequestBodySchema(parameter.Schema);
    }

    private static bool LooksLikeWholeRequestBodySchema(string schema)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(schema);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (root.TryGetProperty("content", out _)
                || root.TryGetProperty("properties", out _)
                || root.TryGetProperty("required", out _))
            {
                return true;
            }

            if (root.TryGetProperty("type", out var type))
            {
                var normalizedType = type.GetString()?.Trim().ToLowerInvariant();
                return normalizedType is "object" or "array";
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool MethodNormallyCarriesBody(HttpMethod method)
        => method is HttpMethod.POST or HttpMethod.PUT or HttpMethod.PATCH;

    private static bool MethodCanCarryGeneratedBody(HttpMethod method)
        => method is HttpMethod.POST or HttpMethod.PUT or HttpMethod.PATCH or HttpMethod.DELETE;

    private static bool MethodForbidsRequestBody(HttpMethod method)
        => method is HttpMethod.GET or HttpMethod.HEAD or HttpMethod.OPTIONS;

    private static bool LooksLikeJsonBody(string body)
    {
        var trimmed = body?.Trim();
        return !string.IsNullOrWhiteSpace(trimmed)
            && ((trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
                || (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal)));
    }

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
    private static string EnsureLlmSourcedTagJson(
        string existingTagsJson,
        TestType testType,
        AiGeneratedTestCaseDto dto)
    {
        var tags = ParseTagList(existingTagsJson);

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
        AddTagIfMissing(tags, typeTag);

        // Ensure markers required by IsLlmSourced.
        foreach (var required in new[] { "auto-generated", "llm-suggested" })
        {
            AddTagIfMissing(tags, required);
        }

        MergeExecutionHintTags(tags, dto);

        if (!HasTagPrefix(tags, RewritePolicyTagPrefix) && ShouldAddMinimalRewritePolicy(dto, tags))
        {
            tags.Add("rewrite-policy:minimal");
        }

        if (!HasTagPrefix(tags, AuthModeTagPrefix) && IsNoAuthIntent(dto, tags))
        {
            tags.Add("auth-mode:none");
        }

        if (!HasTagPrefix(tags, AuthFallbackTagPrefix) && ShouldAllowAuthFallback(dto, tags))
        {
            tags.Add("auth-fallback:allow");
        }

        return JsonSerializer.Serialize(tags, JsonOpts);
    }

    private static List<string> ParseTagList(string existingTagsJson)
    {
        var normalised = NormalizeTagsJson(existingTagsJson);
        try
        {
            return JsonSerializer.Deserialize<List<string>>(normalised, JsonOpts)?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static void AddTagIfMissing(List<string> tags, string tag)
    {
        if (tags == null || string.IsNullOrWhiteSpace(tag))
        {
            return;
        }

        if (!tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            tags.Add(tag);
        }
    }

    private static void AddPrefixedTagIfMissing(List<string> tags, string prefix, string value)
    {
        if (tags == null || string.IsNullOrWhiteSpace(prefix) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        AddTagIfMissing(tags, $"{prefix}{value.Trim()}");
    }

    private static void MergeExecutionHintTags(List<string> tags, AiGeneratedTestCaseDto dto)
    {
        if (tags == null || dto == null)
        {
            return;
        }

        var scenarioKey = !string.IsNullOrWhiteSpace(dto.ScenarioKey)
            ? dto.ScenarioKey
            : NormalizeScenarioLookupKey(dto.Name);
        AddPrefixedTagIfMissing(tags, FlowScenarioKeyTagPrefix, scenarioKey);

        var authMode = ResolveEffectiveAuthMode(dto, tags);
        if (!string.IsNullOrWhiteSpace(authMode))
        {
            AddPrefixedTagIfMissing(tags, AuthModeTagPrefix, authMode);
        }

        var credentialPolicy = dto.ExecutionHints?.CredentialPolicy ?? dto.CredentialPolicy;
        if (!string.IsNullOrWhiteSpace(credentialPolicy))
        {
            AddPrefixedTagIfMissing(tags, CredentialPolicyTagPrefix, credentialPolicy);
        }

        var lockedFields = dto.ExecutionHints?.LockedFields?.Count > 0
            ? dto.ExecutionHints.LockedFields
            : dto.LockedFields;
        foreach (var lockedField in NormalizeStringList(lockedFields))
        {
            AddPrefixedTagIfMissing(tags, CredentialLockTagPrefix, lockedField);
        }

        var flowRequired = dto.ExecutionHints?.FlowRequired ?? dto.FlowRequired;
        if (flowRequired == true)
        {
            AddPrefixedTagIfMissing(tags, FlowRequiredTagPrefix, "true");
        }

        foreach (var dependsOn in NormalizeStringList(dto.ExecutionHints?.DependsOn).Concat(NormalizeStringList(dto.DependsOn)))
        {
            AddPrefixedTagIfMissing(tags, FlowDependsOnTagPrefix, dependsOn);
        }

        foreach (var produces in NormalizeStringList(dto.ExecutionHints?.Produces).Concat(NormalizeStringList(dto.Produces)))
        {
            AddPrefixedTagIfMissing(tags, FlowProducesTagPrefix, produces);
        }

        foreach (var consumes in NormalizeStringList(dto.ExecutionHints?.Consumes).Concat(NormalizeStringList(dto.Consumes)))
        {
            AddPrefixedTagIfMissing(tags, FlowConsumesTagPrefix, consumes);
        }

        foreach (var variable in dto.Variables ?? new List<AiTestCaseVariableDto>())
        {
            AddPrefixedTagIfMissing(tags, FlowProducesTagPrefix, variable?.VariableName);
        }

        foreach (var placeholder in ExtractPlaceholders(dto))
        {
            if (!IsBuiltInRuntimeVariable(placeholder))
            {
                AddPrefixedTagIfMissing(tags, FlowConsumesTagPrefix, placeholder);
            }
        }
    }

    private static List<string> NormalizeStringList(IEnumerable<string> values)
        => values?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();

    private static bool HasTagPrefix(IEnumerable<string> tags, string prefix)
        => tags?.Any(x => !string.IsNullOrWhiteSpace(x)
            && x.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) == true;

    private static bool HasTagValue(IEnumerable<string> tags, string prefix, string value)
        => tags?.Any(x =>
        {
            if (string.IsNullOrWhiteSpace(x) ||
                !x.Trim().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var actual = x.Trim()[prefix.Length..].Trim();
            return string.Equals(NormalizeAuthMode(actual), NormalizeAuthMode(value), StringComparison.OrdinalIgnoreCase)
                || string.Equals(actual, value, StringComparison.OrdinalIgnoreCase);
        }) == true;

    private static bool ShouldAddMinimalRewritePolicy(AiGeneratedTestCaseDto dto, IReadOnlyCollection<string> tags)
    {
        if (HasTagPrefix(tags, FlowRequiredTagPrefix) ||
            HasTagPrefix(tags, FlowDependsOnTagPrefix) ||
            HasTagPrefix(tags, FlowProducesTagPrefix) ||
            HasTagPrefix(tags, FlowConsumesTagPrefix))
        {
            return true;
        }

        if (HasTagPrefix(tags, CredentialLockTagPrefix) ||
            HasNonPreserveCredentialPolicy(tags))
        {
            return true;
        }

        return ExtractPlaceholders(dto)
            .Any(x => !IsBuiltInRuntimeVariable(x));
    }

    private static bool HasNonPreserveCredentialPolicy(IEnumerable<string> tags)
        => tags?.Any(x =>
        {
            if (string.IsNullOrWhiteSpace(x) ||
                !x.Trim().StartsWith(CredentialPolicyTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var policy = x.Trim()[CredentialPolicyTagPrefix.Length..].Trim();
            return !string.Equals(policy, "preserve", StringComparison.OrdinalIgnoreCase);
        }) == true;

    private static bool ShouldAllowAuthFallback(AiGeneratedTestCaseDto dto, IReadOnlyCollection<string> tags)
    {
        if (IsNoAuthIntent(dto, tags))
        {
            return false;
        }

        if (HasTagValue(tags, AuthModeTagPrefix, "required") ||
            string.Equals(ReadAuthModeHeader(dto?.Request?.Headers), "required", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return HasExplicitAuthHeader(dto?.Request?.Headers);
    }

    private static bool IsNoAuthIntent(AiGeneratedTestCaseDto dto, IReadOnlyCollection<string> tags)
    {
        var authRequired = HasAuthRequiredSignal(dto, tags);
        var missingAuthText = HasMissingAuthTextIntent(dto, tags);

        if (HasTagValue(tags, AuthModeTagPrefix, "none") ||
            string.Equals(ReadAuthModeHeader(dto?.Request?.Headers), "none", StringComparison.OrdinalIgnoreCase))
        {
            return !authRequired || missingAuthText;
        }

        return missingAuthText;
    }

    private static bool HasMissingAuthTextIntent(AiGeneratedTestCaseDto dto, IReadOnlyCollection<string> tags)
    {
        var surface = string.Join(' ', new[]
        {
            dto?.Name,
            dto?.Description,
            tags == null ? null : string.Join(' ', tags),
        }.Where(x => !string.IsNullOrWhiteSpace(x)));

        return ContainsAny(surface,
            "no token",
            "without token",
            "missing token",
            "without authorization",
            "no authorization",
            "missing authorization",
            "missing auth",
            "without auth",
            "no auth",
            "no-auth",
            "no_auth");
    }

    private static string ResolveEffectiveAuthMode(AiGeneratedTestCaseDto dto, IReadOnlyCollection<string> tags)
    {
        var declared = NormalizeAuthMode(dto?.ExecutionHints?.AuthMode ?? dto?.AuthMode)
            ?? ReadAuthModeHeader(dto?.Request?.Headers);

        if (string.Equals(declared, "none", StringComparison.OrdinalIgnoreCase)
            && HasAuthRequiredSignal(dto, tags)
            && !HasMissingAuthTextIntent(dto, tags))
        {
            return "required";
        }

        if (string.IsNullOrWhiteSpace(declared) && HasAuthRequiredSignal(dto, tags))
        {
            return "required";
        }

        return declared;
    }

    private static bool HasAuthRequiredSignal(AiGeneratedTestCaseDto dto, IReadOnlyCollection<string> tags)
    {
        if (HasTagValue(tags, AuthModeTagPrefix, "required"))
        {
            return true;
        }

        if (NormalizeStringList(dto?.ExecutionHints?.Consumes)
            .Concat(NormalizeStringList(dto?.Consumes))
            .Any(IsTokenLikeVariableName))
        {
            return true;
        }

        foreach (var placeholder in ExtractPlaceholders(dto))
        {
            if (IsTokenLikeVariableName(placeholder))
            {
                return true;
            }
        }

        return HasExplicitAuthHeader(dto?.Request?.Headers);
    }

    private static string ReadAuthModeHeader(string headersJson)
    {
        if (!TryReadJsonObject(headersJson, out var headers))
        {
            return null;
        }

        foreach (var headerName in AuthModeHeaderNames)
        {
            if (headers.TryGetValue(headerName, out var value))
            {
                return NormalizeAuthMode(value);
            }
        }

        return null;
    }

    private static bool HasExplicitAuthHeader(string headersJson)
    {
        if (!TryReadJsonObject(headersJson, out var headers))
        {
            return false;
        }

        return headers.Any(x => IsAuthHeaderName(x.Key) && !string.IsNullOrWhiteSpace(x.Value));
    }

    private static bool TryReadJsonObject(string json, out Dictionary<string, string> values)
    {
        values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                values[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.GetRawText();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string NormalizeSanitizedHeaders(AiGeneratedTestCaseDto dto)
    {
        if (dto?.Request == null || !TryReadJsonObject(dto.Request.Headers, out var headers))
        {
            return "{}";
        }

        var tags = ParseTagList(dto.Tags);
        MergeExecutionHintTags(tags, dto);

        var noAuthIntent = IsNoAuthIntent(dto, tags);
        var requiresAuth = HasAuthRequiredSignal(dto, tags) && !noAuthIntent;

        if (noAuthIntent)
        {
            foreach (var headerName in headers.Keys.ToList())
            {
                if (IsAuthHeaderName(headerName))
                {
                    headers.Remove(headerName);
                }
            }

            headers["X-Test-Auth-Mode"] = "none";
        }
        else if (requiresAuth)
        {
            foreach (var headerName in AuthModeHeaderNames)
            {
                if (headers.TryGetValue(headerName, out var mode)
                    && string.Equals(NormalizeAuthMode(mode), "none", StringComparison.OrdinalIgnoreCase))
                {
                    headers.Remove(headerName);
                }
            }

            headers["X-Test-Auth-Mode"] = "required";
        }

        return headers.Count == 0 ? "{}" : JsonSerializer.Serialize(headers, JsonOpts);
    }

    private static string NormalizeAuthMode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(" ", string.Empty);

        return normalized switch
        {
            "none" or "noauth" or "disableauth" => "none",
            "optional" => "optional",
            "required" or "default" => "required",
            _ => null,
        };
    }

    private static bool IsAuthHeaderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Equals("Authorization", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase)
            || name.Equals("X-Authorization", StringComparison.OrdinalIgnoreCase)
            || name.Equals("X-Auth", StringComparison.OrdinalIgnoreCase)
            || name.Equals("X-API-Key", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Api-Key", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTokenLikeVariableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim()
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .Replace(".", string.Empty)
            .ToLowerInvariant();

        return normalized.Contains("token", StringComparison.Ordinal)
            || normalized.Contains("jwt", StringComparison.Ordinal)
            || normalized.Contains("bearer", StringComparison.Ordinal)
            || normalized.Contains("apikey", StringComparison.Ordinal)
            || normalized.Contains("authorization", StringComparison.Ordinal)
            || normalized.Contains("auth", StringComparison.Ordinal);
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(value) || needles == null || needles.Length == 0)
        {
            return false;
        }

        return needles.Any(x => !string.IsNullOrWhiteSpace(x)
            && value.Contains(x, StringComparison.OrdinalIgnoreCase));
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

    private static void NormalizeLegacyGeneratedTestCaseFields(IReadOnlyList<AiGeneratedTestCaseDto> testCases)
    {
        if (testCases == null || testCases.Count == 0)
        {
            return;
        }

        foreach (var testCase in testCases)
        {
            if (testCase == null)
            {
                continue;
            }

            if (testCase.Request == null)
            {
                testCase.Request = new AiTestCaseRequestDto();
            }

            if (testCase.Expectation == null)
            {
                testCase.Expectation = new AiTestCaseExpectationDto();
            }

            testCase.Variables ??= new List<AiTestCaseVariableDto>();
            testCase.CoveredRequirementIds ??= new List<Guid>();
            testCase.CoveredRequirementCodes ??= new List<string>();
        }
    }

    private static void MapCoveredRequirementCodesToIds(
        IReadOnlyList<AiGeneratedTestCaseDto> testCases,
        IReadOnlyDictionary<Guid, SrsRequirement> requirementsById)
    {
        if (testCases == null || testCases.Count == 0 || requirementsById == null || requirementsById.Count == 0)
        {
            return;
        }

        var codeToRequirementId = requirementsById.Values
            .Where(x => !string.IsNullOrWhiteSpace(x.RequirementCode))
            .GroupBy(x => x.RequirementCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Id, StringComparer.OrdinalIgnoreCase);

        if (codeToRequirementId.Count == 0)
        {
            return;
        }

        foreach (var testCase in testCases)
        {
            if (testCase == null)
            {
                continue;
            }

            var mappedIds = testCase.CoveredRequirementIds?
                .Where(x => x != Guid.Empty)
                .Distinct()
                .ToList()
                ?? new List<Guid>();

            foreach (var code in testCase.CoveredRequirementCodes ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                if (codeToRequirementId.TryGetValue(code.Trim(), out var requirementId) &&
                    !mappedIds.Contains(requirementId))
                {
                    mappedIds.Add(requirementId);
                }
            }

            if (!string.IsNullOrWhiteSpace(testCase.Expectation?.RequirementCode) &&
                codeToRequirementId.TryGetValue(testCase.Expectation.RequirementCode.Trim(), out var expectationRequirementId) &&
                !mappedIds.Contains(expectationRequirementId))
            {
                mappedIds.Add(expectationRequirementId);
            }

            testCase.CoveredRequirementIds = mappedIds;
        }
    }

    private async Task<IReadOnlyList<AiGeneratedTestCaseDto>> ApplyGeneratedTestCaseQualityFilterAsync(
        TestSuite suite,
        IReadOnlyList<AiGeneratedTestCaseDto> testCases,
        CancellationToken cancellationToken)
    {
        if (testCases == null || testCases.Count == 0)
        {
            return Array.Empty<AiGeneratedTestCaseDto>();
        }

        var metadataByEndpointId = await LoadEndpointMetadataForQualityFilterAsync(
            suite,
            testCases,
            cancellationToken);

        var seenFingerprints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<GeneratedTestCaseQualityCandidate>();
        var droppedCount = 0;

        for (var index = 0; index < testCases.Count; index++)
        {
            var testCase = testCases[index];
            if (!TryPrepareGeneratedTestCaseForQualityFilter(
                    suite,
                    testCase,
                    metadataByEndpointId,
                    index,
                    out var endpointKey,
                    out var reason))
            {
                droppedCount++;
                LogDroppedGeneratedTestCase(index, testCase?.Name, reason);
                continue;
            }

            var fingerprint = BuildGeneratedTestCaseFingerprint(testCase);
            if (!seenFingerprints.Add(fingerprint))
            {
                droppedCount++;
                LogDroppedGeneratedTestCase(index, testCase.Name, "duplicate request/expectation fingerprint");
                continue;
            }

            candidates.Add(new GeneratedTestCaseQualityCandidate(
                testCase,
                index,
                endpointKey,
                GetGeneratedTestCaseEndpointCap(testCase.Request?.HttpMethod)));
        }

        var selected = SelectQualityFilteredTestCases(candidates);
        droppedCount += candidates.Count - selected.Count;

        if (selected.Count == 0)
        {
            throw new ValidationException("All AI-generated test cases were filtered out by backend quality checks.");
        }

        var kept = selected
            .OrderBy(x => x.OriginalIndex)
            .Select(x => x.TestCase)
            .ToList();

        for (var i = 0; i < kept.Count; i++)
        {
            kept[i].OrderIndex = i;
        }

        if (droppedCount > 0)
        {
            _logger.LogWarning(
                "Filtered AI-generated test cases before saving. TestSuiteId={TestSuiteId}, RawCount={RawCount}, KeptCount={KeptCount}, DroppedCount={DroppedCount}",
                suite?.Id,
                testCases.Count,
                kept.Count,
                droppedCount);
        }

        return kept;
    }

    private async Task<Dictionary<Guid, ApiEndpointMetadataDto>> LoadEndpointMetadataForQualityFilterAsync(
        TestSuite suite,
        IReadOnlyList<AiGeneratedTestCaseDto> testCases,
        CancellationToken cancellationToken)
    {
        if (suite?.ApiSpecId is not Guid specId || specId == Guid.Empty)
        {
            return new Dictionary<Guid, ApiEndpointMetadataDto>();
        }

        var endpointIds = testCases
            .Where(x => x?.EndpointId is Guid id && id != Guid.Empty)
            .Select(x => x.EndpointId!.Value)
            .Distinct()
            .ToList();

        if (endpointIds.Count == 0)
        {
            return new Dictionary<Guid, ApiEndpointMetadataDto>();
        }

        var metadata = await _apiEndpointMetadataService.GetEndpointMetadataAsync(
            specId,
            endpointIds,
            cancellationToken);

        return (metadata ?? Array.Empty<ApiEndpointMetadataDto>())
            .GroupBy(x => x.EndpointId)
            .Select(x => x.First())
            .ToDictionary(x => x.EndpointId);
    }

    private bool TryPrepareGeneratedTestCaseForQualityFilter(
        TestSuite suite,
        AiGeneratedTestCaseDto testCase,
        IReadOnlyDictionary<Guid, ApiEndpointMetadataDto> metadataByEndpointId,
        int index,
        out Guid endpointKey,
        out string reason)
    {
        endpointKey = testCase?.EndpointId ?? Guid.Empty;
        reason = null;

        if (testCase == null)
        {
            reason = "test case is null";
            return false;
        }

        if (testCase.EndpointId is not Guid endpointId || endpointId == Guid.Empty)
        {
            reason = "missing endpointId";
            return false;
        }

        ApiEndpointMetadataDto endpoint = null;
        if (suite?.ApiSpecId is Guid specId && specId != Guid.Empty)
        {
            if (metadataByEndpointId == null || !metadataByEndpointId.TryGetValue(endpointId, out endpoint))
            {
                reason = $"unknown endpointId '{endpointId}' for specification '{specId}'";
                return false;
            }
        }

        testCase.Request ??= new AiTestCaseRequestDto();
        testCase.Expectation ??= new AiTestCaseExpectationDto();

        if (string.IsNullOrWhiteSpace(testCase.Request.HttpMethod))
        {
            testCase.Request.HttpMethod = endpoint?.HttpMethod;
        }

        if (string.IsNullOrWhiteSpace(testCase.Request.Url))
        {
            testCase.Request.Url = endpoint?.Path;
        }

        if (!TryParseHttpMethod(testCase.Request.HttpMethod, out var parsedMethod) &&
            (TryParseHttpMethod(testCase.Name, out parsedMethod) ||
             TryParseHttpMethod(testCase.Description, out parsedMethod)))
        {
            testCase.Request.HttpMethod = parsedMethod.ToString();
        }

        if (string.IsNullOrWhiteSpace(testCase.Request.HttpMethod))
        {
            reason = "missing request.httpMethod";
            return false;
        }

        if (string.IsNullOrWhiteSpace(testCase.Request.Url))
        {
            reason = "missing request.url";
            return false;
        }

        if (!TryParseHttpMethod(testCase.Request.HttpMethod, out _))
        {
            reason = $"invalid request.httpMethod '{testCase.Request.HttpMethod}'";
            return false;
        }

        if (endpoint != null &&
            !string.IsNullOrWhiteSpace(endpoint.HttpMethod) &&
            !HttpMethodsEquivalent(testCase.Request.HttpMethod, endpoint.HttpMethod))
        {
            reason = $"request.httpMethod '{testCase.Request.HttpMethod}' does not match endpoint contract '{endpoint.HttpMethod}'";
            return false;
        }

        if (endpoint != null && !EndpointMatchesRequest(testCase, endpoint))
        {
            reason = $"request.url '{testCase.Request.Url}' does not match endpoint contract '{endpoint.Path}'";
            return false;
        }

        if (ContainsInvalidJsonBodyExpression(testCase.Request.Body, testCase.Request.BodyType))
        {
            reason = "JSON request.body contains JavaScript expression";
            return false;
        }

        NormalizeRequestBodyForEndpoint(testCase, endpoint);

        if (endpoint != null && !ExpectedStatusesAreAllowed(testCase, endpoint))
        {
            reason = "expectedStatus is not declared by endpoint contract";
            return false;
        }

        endpointKey = endpointId;
        return true;
    }

    private static bool HttpMethodsEquivalent(string left, string right)
    {
        return TryParseHttpMethod(left, out var leftMethod)
            && TryParseHttpMethod(right, out var rightMethod)
            && leftMethod == rightMethod;
    }

    private async Task<ApiEndpointMetadataDto> ResolveEndpointMetadataForRequestAsync(
        Guid specId,
        AiGeneratedTestCaseDto dto,
        ApiEndpointMetadataDto currentEndpoint,
        CancellationToken cancellationToken)
    {
        if (specId == Guid.Empty
            || dto?.Request == null
            || currentEndpoint == null
            || EndpointMatchesRequest(dto, currentEndpoint))
        {
            return currentEndpoint;
        }

        var allMetadata = await _apiEndpointMetadataService.GetEndpointMetadataAsync(
            specId,
            selectedEndpointIds: null,
            cancellationToken);
        var matched = FindEndpointMetadataByRequest(dto, allMetadata);
        if (matched == null)
        {
            return currentEndpoint;
        }

        dto.EndpointId = matched.EndpointId;
        if (string.IsNullOrWhiteSpace(dto.Request.HttpMethod) && !string.IsNullOrWhiteSpace(matched.HttpMethod))
        {
            dto.Request.HttpMethod = matched.HttpMethod.Trim().ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(dto.Request.Url) && !string.IsNullOrWhiteSpace(matched.Path))
        {
            dto.Request.Url = matched.Path.Trim();
        }

        return matched;
    }

    private static ApiEndpointMetadataDto FindEndpointMetadataByRequest(
        AiGeneratedTestCaseDto dto,
        IEnumerable<ApiEndpointMetadataDto> endpoints)
    {
        if (dto?.Request == null || endpoints == null)
        {
            return null;
        }

        return endpoints
            .Where(endpoint => endpoint != null && EndpointMatchesRequest(dto, endpoint))
            .OrderByDescending(endpoint => string.Equals(
                ExtractComparablePath(dto.Request.Url),
                ExtractComparablePath(endpoint.Path),
                StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    private static bool EndpointMatchesRequest(
        AiGeneratedTestCaseDto dto,
        ApiEndpointMetadataDto endpoint)
    {
        if (dto?.Request == null || endpoint == null)
        {
            return false;
        }

        return HttpMethodsEquivalent(dto.Request.HttpMethod, endpoint.HttpMethod)
            && PathsEquivalent(dto.Request.Url, endpoint.Path);
    }

    private static bool PathsEquivalent(string requestUrl, string endpointPath)
    {
        var requestSegments = SplitPathSegments(requestUrl);
        var endpointSegments = SplitPathSegments(endpointPath);
        if (requestSegments.Count == 0 || endpointSegments.Count == 0 || requestSegments.Count != endpointSegments.Count)
        {
            return false;
        }

        for (var i = 0; i < requestSegments.Count; i++)
        {
            var requestSegment = requestSegments[i];
            var endpointSegment = endpointSegments[i];
            if (IsRouteTemplateSegment(requestSegment) || IsRouteTemplateSegment(endpointSegment))
            {
                continue;
            }

            if (!string.Equals(requestSegment, endpointSegment, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static List<string> SplitPathSegments(string value)
    {
        var path = ExtractComparablePath(value);
        if (string.IsNullOrWhiteSpace(path))
        {
            return new List<string>();
        }

        return path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToList();
    }

    private static string ExtractComparablePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute)
            && (absolute.Scheme == "http" || absolute.Scheme == "https"))
        {
            return absolute.AbsolutePath.Trim('/');
        }

        var splitIndex = trimmed.IndexOfAny(new[] { '?', '#' });
        return (splitIndex >= 0 ? trimmed[..splitIndex] : trimmed).Trim('/');
    }

    private static bool IsRouteTemplateSegment(string segment)
    {
        return !string.IsNullOrWhiteSpace(segment)
            && segment.Length >= 2
            && segment[0] == '{'
            && segment[^1] == '}';
    }

    private static bool ContainsInvalidJsonBodyExpression(string body, string bodyType)
    {
        return !string.IsNullOrWhiteSpace(body)
            && ParseBodyType(bodyType) != BodyType.Raw
            && JavaScriptRepeatExpressionRegex.IsMatch(body);
    }

    private static bool ExpectedStatusesAreAllowed(AiGeneratedTestCaseDto testCase, ApiEndpointMetadataDto endpoint)
    {
        var allowedStatuses = endpoint?.Responses?
            .Select(x => x.StatusCode)
            .Where(code => code >= 100 && code <= 599)
            .Distinct()
            .ToHashSet() ?? new HashSet<int>();

        if (allowedStatuses.Count == 0)
        {
            return true;
        }

        var expectedStatuses = ParseStatusCodesFromExpectation(testCase?.Expectation?.ExpectedStatus);
        return expectedStatuses.Count > 0 && expectedStatuses.All(allowedStatuses.Contains);
    }

    private static IReadOnlyList<GeneratedTestCaseQualityCandidate> SelectQualityFilteredTestCases(
        IReadOnlyList<GeneratedTestCaseQualityCandidate> candidates)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return Array.Empty<GeneratedTestCaseQualityCandidate>();
        }

        var selected = new List<GeneratedTestCaseQualityCandidate>();
        foreach (var group in candidates.GroupBy(x => x.EndpointKey))
        {
            var cap = group.First().EndpointCap;
            var ordered = group.OrderBy(x => x.OriginalIndex).ToList();
            if (ordered.Count <= cap)
            {
                selected.AddRange(ordered);
                continue;
            }

            var selectedForEndpoint = new List<GeneratedTestCaseQualityCandidate>();
            var firstHappyPath = ordered.FirstOrDefault(x => ParseTestType(x.TestCase.TestType) == TestType.HappyPath);
            if (firstHappyPath != null)
            {
                selectedForEndpoint.Add(firstHappyPath);
            }

            foreach (var candidate in ordered
                .Where(x => firstHappyPath == null || !ReferenceEquals(x, firstHappyPath))
                .OrderBy(x => GetGeneratedTestCaseValueRank(x.TestCase))
                .ThenBy(x => GetPriorityRank(x.TestCase.Priority))
                .ThenBy(x => x.OriginalIndex))
            {
                if (selectedForEndpoint.Count >= cap)
                {
                    break;
                }

                selectedForEndpoint.Add(candidate);
            }

            selected.AddRange(selectedForEndpoint);
        }

        return selected;
    }

    private static int GetGeneratedTestCaseEndpointCap(string httpMethod)
    {
        return IsLeanGeneratedMethod(httpMethod)
            ? 3
            : 10;
    }

    private static bool IsLeanGeneratedMethod(string httpMethod)
    {
        return TryParseHttpMethod(httpMethod, out var method)
            && (method == HttpMethod.GET || method == HttpMethod.DELETE);
    }

    private static int GetGeneratedTestCaseValueRank(AiGeneratedTestCaseDto testCase)
    {
        return ParseTestType(testCase?.TestType) switch
        {
            TestType.Negative => 0,
            TestType.Boundary => 1,
            TestType.HappyPath => 2,
            TestType.Security => 3,
            TestType.Performance => 4,
            _ => 5,
        };
    }

    private static int GetPriorityRank(string priority)
    {
        return priority?.Trim().ToLowerInvariant() switch
        {
            "critical" => 0,
            "high" => 1,
            "medium" or "normal" => 2,
            "low" => 3,
            _ => 4,
        };
    }

    private static string BuildGeneratedTestCaseFingerprint(AiGeneratedTestCaseDto testCase)
    {
        return string.Join("|", new[]
        {
            testCase.EndpointId?.ToString("N") ?? string.Empty,
            NormalizeFingerprintText(testCase.Request?.HttpMethod).ToUpperInvariant(),
            NormalizeFingerprintText(testCase.Request?.Url),
            NormalizeJsonOrTextForFingerprint(testCase.Request?.PathParams),
            NormalizeJsonOrTextForFingerprint(testCase.Request?.QueryParams),
            NormalizeJsonOrTextForFingerprint(testCase.Request?.Headers),
            NormalizeFingerprintText(testCase.Request?.BodyType).ToUpperInvariant(),
            NormalizeJsonOrTextForFingerprint(testCase.Request?.Body),
            ParseTestType(testCase.TestType).ToString(),
            string.Join(",", ParseStatusCodesFromExpectation(testCase.Expectation?.ExpectedStatus).OrderBy(x => x)),
            string.Join(",", (testCase.CoveredRequirementIds ?? new List<Guid>()).Where(x => x != Guid.Empty).OrderBy(x => x)),
        });
    }

    private static string NormalizeJsonOrTextForFingerprint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            return JsonSerializer.Serialize(doc.RootElement, JsonOpts);
        }
        catch
        {
            return trimmed;
        }
    }

    private static string NormalizeFingerprintText(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }

    private void LogDroppedGeneratedTestCase(int index, string name, string reason)
    {
        _logger.LogWarning(
            "Dropped AI-generated test case before saving. Index={Index}, Name={Name}, Reason={Reason}",
            index,
            name,
            reason);
    }

    private sealed record GeneratedTestCaseQualityCandidate(
        AiGeneratedTestCaseDto TestCase,
        int OriginalIndex,
        Guid EndpointKey,
        int EndpointCap);

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

    private async Task EnsureRequestBodyProducerAliasVariablesAsync(
        AiGeneratedTestCaseDto dto,
        TestCase testCase,
        DateTimeOffset createdDateTime,
        CancellationToken cancellationToken)
    {
        if (dto == null || testCase == null)
        {
            return;
        }

        var body = testCase.Request?.Body ?? dto.Request?.Body;
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        using JsonDocument document = TryParseJsonDocument(body);
        if (document == null || document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        var existingVariableNames = testCase.Variables
            .Where(x => !string.IsNullOrWhiteSpace(x.VariableName))
            .Select(x => x.VariableName.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var variableName in GetDeclaredProducedVariables(dto))
        {
            if (existingVariableNames.Contains(variableName) ||
                IsBuiltInRuntimeVariable(variableName) ||
                IsTokenVariableName(variableName))
            {
                continue;
            }

            if (!TryFindRequestBodyProducerPath(document.RootElement, variableName, "$", out var jsonPath))
            {
                continue;
            }

            var variable = new TestCaseVariable
            {
                Id = Guid.NewGuid(),
                TestCaseId = testCase.Id,
                VariableName = variableName,
                ExtractFrom = ExtractFrom.RequestBody,
                JsonPath = jsonPath,
                CreatedDateTime = createdDateTime,
            };

            await _testCaseVariableRepository.AddAsync(variable, cancellationToken);
            testCase.Variables.Add(variable);
            existingVariableNames.Add(variableName);
        }
    }

    private static JsonDocument TryParseJsonDocument(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(value.Trim());
        }
        catch
        {
            return null;
        }
    }

    private static List<string> GetDeclaredProducedVariables(AiGeneratedTestCaseDto dto)
    {
        var result = new List<string>();
        foreach (var value in NormalizeStringList(dto?.ExecutionHints?.Produces)
            .Concat(NormalizeStringList(dto?.Produces))
            .Concat(dto?.Variables?
                .Select(x => x?.VariableName)
                .Where(x => !string.IsNullOrWhiteSpace(x)) ?? Enumerable.Empty<string>()))
        {
            if (!result.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(value);
            }
        }

        return result;
    }

    private static bool TryFindRequestBodyProducerPath(
        JsonElement element,
        string variableName,
        string currentPath,
        out string jsonPath)
    {
        jsonPath = null;
        if (element.ValueKind != JsonValueKind.Object || string.IsNullOrWhiteSpace(variableName))
        {
            return false;
        }

        string bestPath = null;
        var bestScore = 0;
        foreach (var property in element.EnumerateObject())
        {
            var propertyPath = $"{currentPath}.{EscapeJsonPathProperty(property.Name)}";
            var score = ScoreSemanticBodyFieldMatch(variableName, property.Name);
            if (score > bestScore && IsExtractableRequestBodyValue(property.Value))
            {
                bestScore = score;
                bestPath = propertyPath;
            }

            if (property.Value.ValueKind == JsonValueKind.Object &&
                TryFindRequestBodyProducerPath(property.Value, variableName, propertyPath, out var nestedPath))
            {
                var nestedScore = ScoreSemanticBodyFieldMatch(variableName, nestedPath.Split('.').LastOrDefault());
                if (nestedScore > bestScore)
                {
                    bestScore = nestedScore;
                    bestPath = nestedPath;
                }
            }
        }

        if (bestScore <= 0 || string.IsNullOrWhiteSpace(bestPath))
        {
            return false;
        }

        jsonPath = bestPath;
        return true;
    }

    private static bool IsExtractableRequestBodyValue(JsonElement element)
        => element.ValueKind is JsonValueKind.String
            or JsonValueKind.Number
            or JsonValueKind.True
            or JsonValueKind.False
            or JsonValueKind.Null
            or JsonValueKind.Object
            or JsonValueKind.Array;

    private static int ScoreSemanticBodyFieldMatch(string variableName, string fieldName)
    {
        var variable = NormalizeSemanticVariableName(variableName);
        var field = NormalizeSemanticVariableName(fieldName);
        if (string.IsNullOrWhiteSpace(variable) || string.IsNullOrWhiteSpace(field))
        {
            return 0;
        }

        if (string.Equals(variable, field, StringComparison.Ordinal))
        {
            return 100;
        }

        if (variable.EndsWith(field, StringComparison.Ordinal))
        {
            return Math.Max(40, field.Length * 4);
        }

        if (field.EndsWith(variable, StringComparison.Ordinal))
        {
            return Math.Max(35, variable.Length * 4);
        }

        return 0;
    }

    private static string NormalizeSemanticVariableName(string value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string EscapeJsonPathProperty(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return propertyName;
        }

        return Regex.IsMatch(propertyName, "^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)
            ? propertyName
            : $"['{propertyName.Replace("'", "\\'", StringComparison.Ordinal)}']";
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
        };

        foreach (var builtIn in BuiltInRuntimeVariableNames)
        {
            producedVariables.Add(builtIn);
        }

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

            var hasSrsTraceability = HasSrsTraceability(dto);
            ValidateRequestShape(dto, endpoint, item.Index);
            ValidateExpectedStatusesAgainstOpenApi(dto, endpoint, item.Index, hasSrsTraceability);
            ValidateAuthorizationHeaderAgainstOpenApi(dto, endpoint, item.Index, hasSrsTraceability);
            ValidateRequestBodyAgainstOpenApi(dto, endpoint, item.Index);
            ValidateVariableDependencies(dto, producedVariables, item.Index);
            RegisterProducedVariables(dto, producedVariables);
        }
    }

    private static void ValidateRequestShape(
        AiGeneratedTestCaseDto dto,
        ApiEndpointMetadataDto endpoint,
        int index)
    {
        var request = dto?.Request;
        if (request == null)
        {
            throw new ValidationException(
                $"AI-generated test case '{dto?.Name ?? $"index {index}"}' is missing request object.");
        }

        if (!TryParseHttpMethod(request.HttpMethod, out _))
        {
            throw new ValidationException(
                $"AI-generated test case '{dto?.Name ?? $"index {index}"}' is missing/invalid request.httpMethod.");
        }

        var url = request.Url?.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ValidationException(
                $"AI-generated test case '{dto?.Name ?? $"index {index}"}' is missing request.url.");
        }

        if (endpoint != null && !EndpointMatchesRequest(dto, endpoint))
        {
            throw new ValidationException(
                $"AI-generated test case '{dto?.Name ?? $"index {index}"}' request '{request.HttpMethod} {request.Url}' " +
                $"does not match endpoint contract '{endpoint.HttpMethod} {endpoint.Path}'.");
        }

        if (request.BodyType != null &&
            ParseBodyType(request.BodyType) == BodyType.JSON &&
            !string.IsNullOrWhiteSpace(request.Body))
        {
            var body = request.Body.Trim();
            if (SingleBraceTokenRegex.IsMatch(body))
            {
                throw new ValidationException(
                    $"AI-generated test case '{dto?.Name ?? $"index {index}"}' contains unresolved token in request.body: '{body}'.");
            }

            // For normal JSON body cases, body must be parseable JSON.
            // If malformed JSON is intended, it should use BodyType=Raw.
            try
            {
                using var _ = JsonDocument.Parse(body);
            }
            catch (JsonException)
            {
                throw new ValidationException(
                    $"AI-generated test case '{dto?.Name ?? $"index {index}"}' has BodyType=JSON but request.body is invalid JSON. " +
                    "Use BodyType=Raw for intentionally malformed payload tests.");
            }
        }
    }

    private static void ValidateRequestBodyAgainstOpenApi(
        AiGeneratedTestCaseDto dto,
        ApiEndpointMetadataDto endpoint,
        int index)
    {
        var issues = RequestSchemaPayloadValidator.Validate(
            dto?.Request?.Body,
            dto?.Request?.BodyType,
            endpoint,
            variables: null,
            expectedStatuses: ParseStatusCodesFromExpectation(dto?.Expectation?.ExpectedStatus),
            testType: dto?.TestType,
            testName: dto?.Name);

        if (issues.Count == 0)
        {
            return;
        }

        var issue = issues[0];
        throw new ValidationException(
            $"AI-generated test case '{dto?.Name ?? $"index {index}"}' has invalid request payload for endpoint " +
            $"'{endpoint?.HttpMethod} {endpoint?.Path}': {issue.Code} at {issue.Target}. " +
            $"Expected {issue.Expected}; actual {issue.Actual}.");
    }

    private static void ValidateExpectedStatusesAgainstOpenApi(
        AiGeneratedTestCaseDto dto,
        ApiEndpointMetadataDto endpoint,
        int index,
        bool hasSrsTraceability)
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
        if (invalid.Count > 0 && !hasSrsTraceability)
        {
            throw new ValidationException(
                $"AI-generated test case '{dto?.Name ?? $"index {index}"}' has expectedStatus [{string.Join(", ", expectedStatuses)}] " +
                $"but OpenAPI for endpoint '{endpoint.HttpMethod} {endpoint.Path}' allows [{string.Join(", ", allowedStatuses.OrderBy(x => x))}].");
        }
    }

    private static void ValidateAuthorizationHeaderAgainstOpenApi(
        AiGeneratedTestCaseDto dto,
        ApiEndpointMetadataDto endpoint,
        int index,
        bool hasSrsTraceability)
    {
        if (endpoint?.IsAuthRelated == true || hasSrsTraceability)
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

    private static bool HasSrsTraceability(AiGeneratedTestCaseDto dto)
    {
        if (dto == null)
        {
            return false;
        }

        if (dto.CoveredRequirementIds?.Any(x => x != Guid.Empty) == true ||
            dto.CoveredRequirementCodes?.Any(x => !string.IsNullOrWhiteSpace(x)) == true)
        {
            return true;
        }

        var expectation = dto.Expectation;
        return !string.IsNullOrWhiteSpace(expectation?.RequirementCode) ||
               expectation?.PrimaryRequirementId is Guid requirementId && requirementId != Guid.Empty ||
               string.Equals(expectation?.ExpectationSource, "Srs", StringComparison.OrdinalIgnoreCase) ||
               ContainsSrsProvenance(expectation?.ExpectedProvenance);
    }

    private static bool ContainsSrsProvenance(string expectedProvenance)
        => !string.IsNullOrWhiteSpace(expectedProvenance) &&
           expectedProvenance.Contains("srs", StringComparison.OrdinalIgnoreCase);

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

    private static SrsRequirement ResolvePrimaryRequirement(
        AiGeneratedTestCaseDto dto,
        IReadOnlyDictionary<Guid, SrsRequirement> requirementsById)
    {
        if (dto == null || requirementsById == null || requirementsById.Count == 0)
        {
            return null;
        }

        if (dto.Expectation?.PrimaryRequirementId is Guid primaryId
            && primaryId != Guid.Empty
            && requirementsById.TryGetValue(primaryId, out var explicitRequirement))
        {
            return explicitRequirement;
        }

        if (!string.IsNullOrWhiteSpace(dto.Expectation?.RequirementCode))
        {
            var byCode = requirementsById.Values.FirstOrDefault(x =>
                string.Equals(
                    x.RequirementCode,
                    dto.Expectation.RequirementCode,
                    StringComparison.OrdinalIgnoreCase));
            if (byCode != null)
            {
                return byCode;
            }
        }

        var firstCovered = dto.CoveredRequirementIds?
            .Where(x => x != Guid.Empty)
            .Distinct()
            .FirstOrDefault(x => requirementsById.ContainsKey(x)) ?? Guid.Empty;

        return firstCovered != Guid.Empty && requirementsById.TryGetValue(firstCovered, out var requirement)
            ? requirement
            : null;
    }

    private static bool IsBuiltInRuntimeVariable(string variableName)
        => !string.IsNullOrWhiteSpace(variableName)
           && BuiltInRuntimeVariableNames.Contains(variableName.Trim());

    private static void ApplyExplicitFlowDependenciesFromTags(
        IReadOnlyCollection<TestCase> materializedTestCases,
        IReadOnlyCollection<TestCase> existingTestCases)
    {
        if (materializedTestCases == null || materializedTestCases.Count == 0)
        {
            return;
        }

        var scenarioKeyToTestCaseId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var existing in existingTestCases ?? Array.Empty<TestCase>())
        {
            RegisterScenarioLookupKeys(scenarioKeyToTestCaseId, existing);
        }

        foreach (var current in materializedTestCases)
        {
            RegisterScenarioLookupKeys(scenarioKeyToTestCaseId, current);
        }

        foreach (var testCase in materializedTestCases)
        {
            var depKeys = GetMultiTagValues(testCase.Tags, FlowDependsOnTagPrefix);
            foreach (var depKey in depKeys)
            {
                if (!TryResolveScenarioDependencyKey(scenarioKeyToTestCaseId, depKey, out var depTestCaseId)
                    || depTestCaseId == testCase.Id
                    || testCase.Dependencies.Any(x => x.DependsOnTestCaseId == depTestCaseId))
                {
                    continue;
                }

                testCase.Dependencies.Add(new TestCaseDependency
                {
                    Id = Guid.NewGuid(),
                    TestCaseId = testCase.Id,
                    DependsOnTestCaseId = depTestCaseId,
                });
            }
        }
    }

    private static void RegisterScenarioLookupKeys(
        IDictionary<string, Guid> lookup,
        TestCase testCase)
    {
        if (lookup == null || testCase == null)
        {
            return;
        }

        var candidates = new[]
        {
            GetSingleTagValue(testCase.Tags, FlowScenarioKeyTagPrefix),
            testCase.Name,
            NormalizeScenarioLookupKey(GetSingleTagValue(testCase.Tags, FlowScenarioKeyTagPrefix)),
            NormalizeScenarioLookupKey(testCase.Name),
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                lookup[candidate.Trim()] = testCase.Id;
            }
        }
    }

    private static bool TryResolveScenarioDependencyKey(
        IReadOnlyDictionary<string, Guid> lookup,
        string dependencyKey,
        out Guid testCaseId)
    {
        testCaseId = Guid.Empty;
        if (lookup == null || string.IsNullOrWhiteSpace(dependencyKey))
        {
            return false;
        }

        var candidates = new[]
        {
            dependencyKey.Trim(),
            NormalizeScenarioLookupKey(dependencyKey),
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && lookup.TryGetValue(candidate, out testCaseId))
            {
                return true;
            }
        }

        return false;
    }

    private static string GetSingleTagValue(string tagsJson, string prefix)
        => GetMultiTagValues(tagsJson, prefix).FirstOrDefault();

    private static List<string> GetMultiTagValues(string tagsJson, string prefix)
    {
        var tags = ParseTagList(tagsJson);
        var values = new List<string>();
        foreach (var tag in tags)
        {
            if (!tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = tag[prefix.Length..].Trim();
            if (!string.IsNullOrWhiteSpace(value)
                && !values.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                values.Add(value);
            }
        }

        return values;
    }

    private static string NormalizeScenarioLookupKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var chars = value
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        var normalized = new string(chars);
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Trim('-');
    }

    private static void RegisterProducedVariables(
        AiGeneratedTestCaseDto dto,
        HashSet<string> producedVariables)
    {
        if (dto == null || producedVariables == null)
        {
            return;
        }

        foreach (var produces in NormalizeStringList(dto.ExecutionHints?.Produces).Concat(NormalizeStringList(dto.Produces)))
        {
            producedVariables.Add(produces);
            if (IsTokenVariableName(produces))
            {
                foreach (var alias in AuthTokenAliases)
                {
                    producedVariables.Add(alias);
                }
            }
        }

        foreach (var variable in dto.Variables ?? new List<AiTestCaseVariableDto>())
        {
            if (string.IsNullOrWhiteSpace(variable?.VariableName))
            {
                continue;
            }

            producedVariables.Add(variable.VariableName.Trim());
            if (IsTokenVariableName(variable.VariableName))
            {
                foreach (var alias in AuthTokenAliases)
                {
                    producedVariables.Add(alias);
                }
            }
        }
    }

    private static readonly string[] AuthTokenAliases =
    {
        "authToken",
        "accessToken",
        "access_token",
        "token",
        "jwt",
        "bearerToken",
        "bearer_token",
        "idToken",
        "id_token",
        "sessionToken",
        "session_token",
    };

    private static bool IsTokenVariableName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = new string(value.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        return normalized.EndsWith("token", StringComparison.Ordinal)
            || normalized is "jwt" or "bearer" or "authorization";
    }
}
