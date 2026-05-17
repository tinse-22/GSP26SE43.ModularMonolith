using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public class LlmSuggestionReviewService : ILlmSuggestionReviewService
{
    private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions();

    static LlmSuggestionReviewService()
    {
        JsonOpts.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        JsonOpts.WriteIndented = false;
    }

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseRequest, Guid> _requestRepository;
    private readonly IRepository<TestCaseExpectation, Guid> _expectationRepository;
    private readonly IRepository<TestCaseVariable, Guid> _variableRepository;
    private readonly IRepository<TestCaseDependency, Guid> _dependencyRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;
    private readonly IRepository<TestSuiteVersion, Guid> _versionRepository;
    private readonly IRepository<TestCaseRequirementLink, Guid> _linkRepository;
    private readonly IRepository<SrsRequirement, Guid> _srsRequirementRepository;
    private readonly ILlmSuggestionMaterializer _materializer;
    private readonly IApiTestOrderGateService _gateService;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;
    private readonly ILogger<LlmSuggestionReviewService> _logger;

    public LlmSuggestionReviewService(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository,
        IRepository<TestCase, Guid> testCaseRepository,
        IRepository<TestCaseRequest, Guid> requestRepository,
        IRepository<TestCaseExpectation, Guid> expectationRepository,
        IRepository<TestCaseVariable, Guid> variableRepository,
        IRepository<TestCaseDependency, Guid> dependencyRepository,
        IRepository<TestCaseChangeLog, Guid> changeLogRepository,
        IRepository<TestSuiteVersion, Guid> versionRepository,
        IRepository<TestCaseRequirementLink, Guid> linkRepository,
        IRepository<SrsRequirement, Guid> srsRequirementRepository,
        ILlmSuggestionMaterializer materializer,
        IApiTestOrderGateService gateService,
        ISubscriptionLimitGatewayService subscriptionLimitService,
        ILogger<LlmSuggestionReviewService> logger)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
        _testCaseRepository = testCaseRepository;
        _requestRepository = requestRepository;
        _expectationRepository = expectationRepository;
        _variableRepository = variableRepository;
        _dependencyRepository = dependencyRepository;
        _changeLogRepository = changeLogRepository;
        _versionRepository = versionRepository;
        _linkRepository = linkRepository;
        _srsRequirementRepository = srsRequirementRepository;
        _materializer = materializer;
        _gateService = gateService;
        _subscriptionLimitService = subscriptionLimitService;
        _logger = logger;
    }

    public Task RejectAsync(
        LlmSuggestion suggestion,
        Guid currentUserId,
        string reviewNotes,
        CancellationToken cancellationToken = default)
    {
        return RejectManyAsync(new[] { suggestion }, currentUserId, reviewNotes, cancellationToken);
    }

    public async Task RejectManyAsync(
        IReadOnlyCollection<LlmSuggestion> suggestions,
        Guid currentUserId,
        string reviewNotes,
        CancellationToken cancellationToken = default)
    {
        ValidationException.Requires(currentUserId != Guid.Empty, "CurrentUserId là bắt buộc.");

        var reviewSuggestions = PrepareSuggestions(suggestions);
        if (reviewSuggestions.Count == 0)
        {
            return;
        }

        EnsureSuggestionsArePending(reviewSuggestions);

        var now = DateTimeOffset.UtcNow;

        try
        {
            await _suggestionRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                foreach (var suggestion in reviewSuggestions)
                {
                    suggestion.ReviewStatus = ReviewStatus.Rejected;
                    suggestion.ReviewedById = currentUserId;
                    suggestion.ReviewedAt = now;
                    suggestion.ReviewNotes = reviewNotes;
                    suggestion.UpdatedDateTime = now;
                    suggestion.RowVersion = Guid.NewGuid().ToByteArray();

                    await _suggestionRepository.UpdateAsync(suggestion, ct);
                }

                await _suggestionRepository.UnitOfWork.SaveChangesAsync(ct);
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (_suggestionRepository.IsDbUpdateConcurrencyException(ex))
        {
            throw new ConflictException(
                "CONCURRENCY_CONFLICT",
                "Suggestion đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
        }

        _logger.LogInformation(
            "Rejected {SuggestionCount} LLM suggestion(s). SuggestionIds={SuggestionIds}, ActorUserId={ActorUserId}",
            reviewSuggestions.Count,
            reviewSuggestions.Select(x => x.Id).ToArray(),
            currentUserId);
    }

    public async Task<LlmSuggestionApprovalBatchResult> ApproveManyAsync(
        TestSuite suite,
        IReadOnlyCollection<LlmSuggestionApprovalItem> approvals,
        Guid currentUserId,
        CancellationToken cancellationToken = default)
    {
        var totalStopwatch = Stopwatch.StartNew();
        long limitCheckMs = 0;
        long loadExistingTestCasesMs = 0;
        long materializeMs = 0;
        long dependencyEnrichMs = 0;
        long dbSaveMs = 0;

        ValidationException.Requires(suite != null, "TestSuite là bắt buộc.");
        ValidationException.Requires(currentUserId != Guid.Empty, "CurrentUserId là bắt buộc.");

        var approvalItems = PrepareApprovalItems(approvals);

        if (approvalItems.Count == 0)
        {
            return new LlmSuggestionApprovalBatchResult();
        }

        ValidationException.Requires(
            approvalItems.All(x => x.Suggestion.TestSuiteId == suite.Id),
            "Tất cả suggestions phải thuộc về test suite được review.");
        EnsureSuggestionsAreApprovableForMaterialization(approvalItems.Select(x => x.Suggestion));

        var loadExistingStopwatch = Stopwatch.StartNew();
        var previouslyApprovedSuggestions = (await _suggestionRepository.ToListAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == suite.Id
                    && !x.IsDeleted
                    && x.AppliedTestCaseId.HasValue
                    && (x.ReviewStatus == ReviewStatus.Approved
                        || x.ReviewStatus == ReviewStatus.ModifiedAndApproved))))
            ?? new List<LlmSuggestion>();

        var candidateAppliedTestCaseIds = previouslyApprovedSuggestions
            .Where(x => x != null && x.AppliedTestCaseId.HasValue)
            .Select(x => x.AppliedTestCaseId.Value)
            .Distinct()
            .ToList();

        var reusableAppliedTestCaseIdSet = new HashSet<Guid>();
        if (candidateAppliedTestCaseIds.Count > 0)
        {
            var reusableAppliedTestCases = await _testCaseRepository.ToListAsync(
                _testCaseRepository.GetQueryableSet()
                    .Where(x => candidateAppliedTestCaseIds.Contains(x.Id) && !x.IsDeleted));

            foreach (var testCase in reusableAppliedTestCases ?? Enumerable.Empty<TestCase>())
            {
                reusableAppliedTestCaseIdSet.Add(testCase.Id);
            }
        }

        var fingerprintToAppliedTestCaseId = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var approvedSuggestion in previouslyApprovedSuggestions)
        {
            if (!approvedSuggestion.AppliedTestCaseId.HasValue)
            {
                continue;
            }

            if (!reusableAppliedTestCaseIdSet.Contains(approvedSuggestion.AppliedTestCaseId.Value))
            {
                continue;
            }

            var fingerprint = BuildApprovalFingerprint(approvedSuggestion, modifiedContent: null);
            if (!fingerprintToAppliedTestCaseId.ContainsKey(fingerprint))
            {
                fingerprintToAppliedTestCaseId[fingerprint] = approvedSuggestion.AppliedTestCaseId.Value;
            }
        }

        var pendingFingerprintSet = new HashSet<string>(fingerprintToAppliedTestCaseId.Keys, StringComparer.Ordinal);
        var potentialNewCount = 0;
        foreach (var approvalItem in approvalItems)
        {
            var fingerprint = BuildApprovalFingerprint(approvalItem.Suggestion, approvalItem.ModifiedContent);
            if (pendingFingerprintSet.Add(fingerprint))
            {
                potentialNewCount++;
            }
        }

        if (potentialNewCount > 0)
        {
            var limitStopwatch = Stopwatch.StartNew();
            var limitCheck = await _subscriptionLimitService.CheckLimitAsync(
                currentUserId,
                LimitType.MaxTestCasesPerSuite,
                potentialNewCount,
                cancellationToken);
            limitStopwatch.Stop();
            limitCheckMs = limitStopwatch.ElapsedMilliseconds;

            if (!limitCheck.IsAllowed)
            {
                throw new ValidationException(
                    $"Đã vượt quá giới hạn test case cho gói subscription. {limitCheck.DenialReason}");
            }
        }

        var approvedOrder = await _gateService.RequireApprovedOrderAsync(suite.Id, cancellationToken);
        var orderItemMap = approvedOrder.ToDictionary(x => x.EndpointId);

        // Pre-load valid SRS requirement IDs for this suite to validate links during approve.
        var validRequirementIds = new HashSet<Guid>();
        if (suite.SrsDocumentId.HasValue)
        {
            var reqIds = await _srsRequirementRepository.ToListAsync(
                _srsRequirementRepository.GetQueryableSet()
                    .Where(x => x.SrsDocumentId == suite.SrsDocumentId.Value)
                    .Select(x => x.Id));
            foreach (var id in reqIds)
            {
                validRequirementIds.Add(id);
            }
        }

        var existingTestCases = (await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == suite.Id && !x.IsDeleted)))
            ?? new List<TestCase>();

        var existingTestCaseIds = existingTestCases.Select(x => x.Id).ToList();
        var existingProducerVariables = existingTestCaseIds.Count == 0
            ? new List<TestCaseVariable>()
            : (await _variableRepository.ToListAsync(
                _variableRepository.GetQueryableSet().Where(x => existingTestCaseIds.Contains(x.TestCaseId))))
                ?? new List<TestCaseVariable>();
        loadExistingStopwatch.Stop();
        loadExistingTestCasesMs = loadExistingStopwatch.ElapsedMilliseconds;

        var now = DateTimeOffset.UtcNow;
        var nextOrderIndex = existingTestCases.Count == 0
            ? 0
            : existingTestCases.Max(x => x.OrderIndex) + 1;
        var materializedTestCases = new List<TestCase>(approvalItems.Count);
        var materializedItems = new List<MaterializedSuggestionItem>(approvalItems.Count);

        try
        {
            await _testCaseRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                var materializeStopwatch = Stopwatch.StartNew();
                foreach (var approvalItem in approvalItems)
                {
                    var suggestion = approvalItem.Suggestion;
                    var isModify = approvalItem.ModifiedContent != null;
                    var approvalFingerprint = BuildApprovalFingerprint(suggestion, approvalItem.ModifiedContent);

                    if (fingerprintToAppliedTestCaseId.TryGetValue(approvalFingerprint, out var existingAppliedTestCaseId))
                    {
                        suggestion.ReviewStatus = isModify
                            ? ReviewStatus.ModifiedAndApproved
                            : ReviewStatus.Approved;
                        suggestion.ReviewedById = currentUserId;
                        suggestion.ReviewedAt = now;
                        suggestion.ReviewNotes = approvalItem.ReviewNotes;
                        suggestion.AppliedTestCaseId = existingAppliedTestCaseId;
                        suggestion.UpdatedDateTime = now;
                        suggestion.RowVersion = Guid.NewGuid().ToByteArray();

                        await _suggestionRepository.UpdateAsync(suggestion, ct);
                        continue;
                    }

                    ApiOrderItemModel orderItem = null;
                    if (suggestion.EndpointId.HasValue)
                    {
                        orderItemMap.TryGetValue(suggestion.EndpointId.Value, out orderItem);
                    }

                    var testCase = isModify
                        ? MaterializeFromModifiedContent(suggestion, approvalItem.ModifiedContent, orderItem, nextOrderIndex++)
                        : _materializer.MaterializeFromSuggestion(suggestion, orderItem, nextOrderIndex++);

                    suggestion.ReviewStatus = isModify
                        ? ReviewStatus.ModifiedAndApproved
                        : ReviewStatus.Approved;
                    suggestion.ReviewedById = currentUserId;
                    suggestion.ReviewedAt = now;
                    suggestion.ReviewNotes = approvalItem.ReviewNotes;
                    suggestion.AppliedTestCaseId = testCase.Id;
                    suggestion.UpdatedDateTime = now;
                    suggestion.RowVersion = Guid.NewGuid().ToByteArray();

                    testCase.CreatedDateTime = now;
                    materializedTestCases.Add(testCase);
                    materializedItems.Add(new MaterializedSuggestionItem(suggestion, testCase, isModify));
                    fingerprintToAppliedTestCaseId[approvalFingerprint] = testCase.Id;
                }

                if (materializedTestCases.Count > 0)
                {
                    var dependencyEnrichStopwatch = Stopwatch.StartNew();
                    var enrichment = GeneratedTestCaseDependencyEnricher.Enrich(
                        materializedTestCases,
                        approvedOrder,
                        existingTestCases,
                        existingProducerVariables);
                    dependencyEnrichStopwatch.Stop();
                    dependencyEnrichMs = dependencyEnrichStopwatch.ElapsedMilliseconds;

                    foreach (var existingProducerVariable in enrichment.ExistingProducerVariablesToPersist)
                    {
                        await _variableRepository.AddAsync(existingProducerVariable, ct);
                    }

                    foreach (var item in materializedItems)
                    {
                        var testCase = item.TestCase;
                        await _testCaseRepository.AddAsync(testCase, ct);
                        await _requestRepository.AddAsync(testCase.Request, ct);
                        await _expectationRepository.AddAsync(testCase.Expectation, ct);

                        foreach (var variable in testCase.Variables)
                        {
                            await _variableRepository.AddAsync(variable, ct);
                        }

                        foreach (var dependency in testCase.Dependencies)
                        {
                            await _dependencyRepository.AddAsync(dependency, ct);
                        }

                        await _changeLogRepository.AddAsync(new TestCaseChangeLog
                        {
                            Id = Guid.NewGuid(),
                            TestCaseId = testCase.Id,
                            ChangedById = currentUserId,
                            ChangeType = TestCaseChangeType.Created,
                            OldValue = null,
                            NewValue = JsonSerializer.Serialize(new
                            {
                                testCase.Name,
                                testCase.TestType,
                                testCase.EndpointId,
                                testCase.OrderIndex,
                                VariableCount = testCase.Variables.Count,
                                DependencyCount = testCase.Dependencies.Count,
                                SuggestionId = item.Suggestion.Id,
                            }, JsonOpts),
                            ChangeReason = BuildChangeReason(item.Suggestion.Id, item.IsModify, approvalItems.Count > 1),
                            VersionAfterChange = 1,
                            CreatedDateTime = now,
                        }, ct);

                        // Create traceability links from suggestion's CoveredRequirementIds.
                        var coveredIds = LlmSuggestionModel.DeserializeGuidListStatic(item.Suggestion.CoveredRequirementIds);
                        Guid? primaryReqId = null;
                        foreach (var reqId in coveredIds.Distinct())
                        {
                            if (reqId == Guid.Empty) continue;
                            if (validRequirementIds.Count > 0 && !validRequirementIds.Contains(reqId))
                            {
                                _logger.LogWarning(
                                    "coveredRequirementId {ReqId} for suggestion '{SuggestionId}' does not belong to SrsDocumentId={SrsDocumentId}. Skipping link.",
                                    reqId, item.Suggestion.Id, suite.SrsDocumentId);
                                continue;
                            }

                            await _linkRepository.AddAsync(new TestCaseRequirementLink
                            {
                                Id = Guid.NewGuid(),
                                TestCaseId = testCase.Id,
                                SrsRequirementId = reqId,
                                TraceabilityScore = 1.0f,
                                MappingRationale = "Auto-linked from LLM suggestion on approve.",
                            }, ct);

                            primaryReqId ??= reqId;
                        }

                        if (primaryReqId.HasValue && testCase.PrimaryRequirementId == null)
                        {
                            testCase.PrimaryRequirementId = primaryReqId;
                            await _testCaseRepository.UpdateAsync(testCase, ct);
                        }

                        await _suggestionRepository.UpdateAsync(item.Suggestion, ct);
                    }
                }

                materializeStopwatch.Stop();
                materializeMs = materializeStopwatch.ElapsedMilliseconds;

                await _versionRepository.AddAsync(new TestSuiteVersion
                {
                    Id = Guid.NewGuid(),
                    TestSuiteId = suite.Id,
                    VersionNumber = suite.Version + 1,
                    ChangedById = currentUserId,
                    ChangeType = VersionChangeType.TestCasesModified,
                    ChangeDescription = BuildSuiteChangeDescription(approvalItems),
                    TestCaseOrderSnapshot = JsonSerializer.Serialize(
                        existingTestCases
                            .Concat(materializedTestCases)
                            .OrderBy(x => x.OrderIndex)
                            .Select(x => new { x.Id, x.EndpointId, x.Name, x.OrderIndex })
                            .ToList(),
                        JsonOpts),
                    ApprovalStatusSnapshot = suite.ApprovalStatus,
                    CreatedDateTime = now,
                }, ct);

                suite.Version += 1;
                if (suite.Status != TestSuiteStatus.Archived &&
                    (existingTestCases.Count > 0 || materializedTestCases.Count > 0))
                {
                    suite.Status = TestSuiteStatus.Ready;
                }

                suite.LastModifiedById = currentUserId;
                suite.UpdatedDateTime = now;
                suite.RowVersion = Guid.NewGuid().ToByteArray();
                await _suiteRepository.UpdateAsync(suite, ct);

                var dbSaveStopwatch = Stopwatch.StartNew();
                await _testCaseRepository.UnitOfWork.SaveChangesAsync(ct);
                dbSaveStopwatch.Stop();
                dbSaveMs = dbSaveStopwatch.ElapsedMilliseconds;
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (_suggestionRepository.IsDbUpdateConcurrencyException(ex))
        {
            throw new ConflictException(
                "CONCURRENCY_CONFLICT",
                "Suggestion đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
        }

        if (materializedTestCases.Count > 0)
        {
            await _subscriptionLimitService.IncrementUsageAsync(
                new IncrementUsageRequest
                {
                    UserId = currentUserId,
                    LimitType = LimitType.MaxTestCasesPerSuite,
                    IncrementValue = materializedTestCases.Count,
                },
                cancellationToken);
        }

        totalStopwatch.Stop();
        _logger.LogInformation(
            "LLM suggestion materialization metrics. TestSuiteId={TestSuiteId}, SuggestionReviewPath={SuggestionReviewPath}, SuggestionCount={SuggestionCount}, MaterializedCount={MaterializedCount}, LoadExistingTestCasesMs={LoadExistingTestCasesMs}, LimitCheckMs={LimitCheckMs}, MaterializeMs={MaterializeMs}, DependencyEnrichMs={DependencyEnrichMs}, DbSaveMs={DbSaveMs}, NormalPathMs={NormalPathMs}, BulkPathMs={BulkPathMs}, BulkPathEnabled={BulkPathEnabled}, SuggestionIds={SuggestionIds}, ActorUserId={ActorUserId}",
            suite.Id,
            "normal",
            approvalItems.Count,
            materializedTestCases.Count,
            loadExistingTestCasesMs,
            limitCheckMs,
            materializeMs,
            dependencyEnrichMs,
            dbSaveMs,
            totalStopwatch.ElapsedMilliseconds,
            0,
            false,
            approvalItems.Select(x => x.Suggestion.Id).ToArray(),
            currentUserId);

        return new LlmSuggestionApprovalBatchResult
        {
            Suggestions = approvalItems.Select(x => x.Suggestion).ToList(),
            TestCases = materializedTestCases,
        };
    }

    private sealed class MaterializedSuggestionItem
    {
        public MaterializedSuggestionItem(LlmSuggestion suggestion, TestCase testCase, bool isModify)
        {
            Suggestion = suggestion;
            TestCase = testCase;
            IsModify = isModify;
        }

        public LlmSuggestion Suggestion { get; }

        public TestCase TestCase { get; }

        public bool IsModify { get; }
    }

    private TestCase MaterializeFromModifiedContent(
        LlmSuggestion suggestion,
        EditableLlmSuggestionInput modifiedContent,
        ApiOrderItemModel orderItem,
        int orderIndex)
    {
        suggestion.ModifiedContent = JsonSerializer.Serialize(modifiedContent, JsonOpts);
        return _materializer.MaterializeFromModifiedContent(suggestion, modifiedContent, orderItem, orderIndex);
    }

    private static string BuildChangeReason(Guid suggestionId, bool isModify, bool isBulk)
    {
        if (isBulk)
        {
            return isModify
                ? $"Modified and approved from bulk LLM suggestion review (SuggestionId={suggestionId})"
                : $"Approved from bulk LLM suggestion review (SuggestionId={suggestionId})";
        }

        return isModify
            ? $"Modified and approved from LLM suggestion review (SuggestionId={suggestionId})"
            : $"Approved from LLM suggestion review (SuggestionId={suggestionId})";
    }

    private static string BuildSuiteChangeDescription(IReadOnlyCollection<LlmSuggestionApprovalItem> approvals)
    {
        var modifiedCount = approvals.Count(x => x.ModifiedContent != null);
        var approvedCount = approvals.Count - modifiedCount;

        if (approvals.Count == 1)
        {
            var suggestionId = approvals.First().Suggestion.Id;
            return modifiedCount == 1
                ? $"Modified and approved 1 LLM suggestion as test case (SuggestionId={suggestionId})."
                : $"Approved 1 LLM suggestion as test case (SuggestionId={suggestionId}).";
        }

        return $"Bulk reviewed {approvals.Count} LLM suggestion(s): approved {approvedCount}, modified and approved {modifiedCount}.";
    }

    private static List<LlmSuggestion> PrepareSuggestions(IReadOnlyCollection<LlmSuggestion> suggestions)
    {
        var normalizedSuggestions = suggestions?
            .Where(x => x != null)
            .OrderBy(x => x.DisplayOrder)
            .ToList() ?? new List<LlmSuggestion>();

        EnsureNoDuplicateSuggestions(normalizedSuggestions.Select(x => x.Id));

        return normalizedSuggestions;
    }

    private static List<LlmSuggestionApprovalItem> PrepareApprovalItems(
        IReadOnlyCollection<LlmSuggestionApprovalItem> approvals)
    {
        var approvalItems = approvals?
            .Where(x => x?.Suggestion != null)
            .OrderBy(x => x.Suggestion.DisplayOrder)
            .ToList() ?? new List<LlmSuggestionApprovalItem>();

        EnsureNoDuplicateSuggestions(approvalItems.Select(x => x.Suggestion.Id));

        return approvalItems;
    }

    private static void EnsureSuggestionsArePending(IEnumerable<LlmSuggestion> suggestions)
    {
        ValidationException.Requires(
            suggestions.All(x => x.ReviewStatus == ReviewStatus.Pending),
            "Chỉ có thể review suggestions đang Pending trong shared review service.");
    }

    private static void EnsureSuggestionsAreApprovableForMaterialization(IEnumerable<LlmSuggestion> suggestions)
    {
        ValidationException.Requires(
            suggestions.All(x =>
                x.ReviewStatus == ReviewStatus.Pending
                || ((x.ReviewStatus == ReviewStatus.Approved
                        || x.ReviewStatus == ReviewStatus.ModifiedAndApproved)
                    && !x.AppliedTestCaseId.HasValue)),
            "Chỉ có thể materialize suggestions ở trạng thái Pending, hoặc Approved/ModifiedAndApproved nhưng chưa có AppliedTestCaseId.");
    }

    private static void EnsureNoDuplicateSuggestions(IEnumerable<Guid> suggestionIds)
    {
        var duplicateIds = suggestionIds
            .GroupBy(x => x)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        ValidationException.Requires(
            duplicateIds.Count == 0,
            "Danh sách suggestion review không được chứa phần tử trùng lặp.");
    }

    private static string BuildApprovalFingerprint(
        LlmSuggestion suggestion,
        EditableLlmSuggestionInput modifiedContent)
    {
        var fingerprintPayload = new
        {
            endpointId = suggestion.EndpointId,
            suggestionType = suggestion.SuggestionType.ToString(),
            testType = (modifiedContent?.TestType ?? suggestion.TestType.ToString())?.Trim(),
            priority = (modifiedContent?.Priority ?? suggestion.Priority.ToString())?.Trim(),
            name = (modifiedContent?.Name ?? suggestion.SuggestedName)?.Trim(),
            description = (modifiedContent?.Description ?? suggestion.SuggestedDescription)?.Trim(),
            tags = NormalizeJson(modifiedContent?.Tags != null
                ? JsonSerializer.Serialize(modifiedContent.Tags, JsonOpts)
                : suggestion.SuggestedTags),
            request = NormalizeJson(modifiedContent?.Request != null
                ? JsonSerializer.Serialize(modifiedContent.Request, JsonOpts)
                : suggestion.SuggestedRequest),
            expectation = NormalizeJson(modifiedContent?.Expectation != null
                ? JsonSerializer.Serialize(modifiedContent.Expectation, JsonOpts)
                : suggestion.SuggestedExpectation),
            variables = NormalizeJson(modifiedContent?.Variables != null
                ? JsonSerializer.Serialize(modifiedContent.Variables, JsonOpts)
                : suggestion.SuggestedVariables),
        };

        return JsonSerializer.Serialize(fingerprintPayload, JsonOpts);
    }

    private static string NormalizeJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return JsonSerializer.Serialize(document.RootElement, JsonOpts);
        }
        catch
        {
            return value.Trim();
        }
    }
}
