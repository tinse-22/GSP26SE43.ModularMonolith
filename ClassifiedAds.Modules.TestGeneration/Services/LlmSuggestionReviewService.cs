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
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public class LlmSuggestionReviewService : ILlmSuggestionReviewService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;
    private readonly IRepository<TestCase, Guid> _testCaseRepository;
    private readonly IRepository<TestCaseRequest, Guid> _requestRepository;
    private readonly IRepository<TestCaseExpectation, Guid> _expectationRepository;
    private readonly IRepository<TestCaseVariable, Guid> _variableRepository;
    private readonly IRepository<TestCaseChangeLog, Guid> _changeLogRepository;
    private readonly IRepository<TestSuiteVersion, Guid> _versionRepository;
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
        IRepository<TestCaseChangeLog, Guid> changeLogRepository,
        IRepository<TestSuiteVersion, Guid> versionRepository,
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
        _changeLogRepository = changeLogRepository;
        _versionRepository = versionRepository;
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
        EnsureSuggestionsArePending(approvalItems.Select(x => x.Suggestion));

        var limitCheck = await _subscriptionLimitService.CheckLimitAsync(
            currentUserId,
            LimitType.MaxTestCasesPerSuite,
            approvalItems.Count,
            cancellationToken);

        if (!limitCheck.IsAllowed)
        {
            throw new ValidationException(
                $"Đã vượt quá giới hạn test case cho gói subscription. {limitCheck.DenialReason}");
        }

        var approvedOrder = await _gateService.RequireApprovedOrderAsync(suite.Id, cancellationToken);
        var orderItemMap = approvedOrder.ToDictionary(x => x.EndpointId);

        var existingTestCases = await _testCaseRepository.ToListAsync(
            _testCaseRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == suite.Id));

        var now = DateTimeOffset.UtcNow;
        var nextOrderIndex = existingTestCases.Count == 0
            ? 0
            : existingTestCases.Max(x => x.OrderIndex) + 1;
        var materializedTestCases = new List<TestCase>(approvalItems.Count);

        try
        {
            await _testCaseRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                foreach (var approvalItem in approvalItems)
                {
                    var suggestion = approvalItem.Suggestion;
                    var isModify = approvalItem.ModifiedContent != null;

                    ApiOrderItemModel orderItem = null;
                    if (suggestion.EndpointId.HasValue)
                    {
                        orderItemMap.TryGetValue(suggestion.EndpointId.Value, out orderItem);
                    }

                    var testCase = isModify
                        ? MaterializeFromModifiedContent(suggestion, approvalItem.ModifiedContent, orderItem, nextOrderIndex++)
                        : _materializer.MaterializeFromSuggestion(suggestion, orderItem, nextOrderIndex++);

                    testCase.CreatedDateTime = now;
                    materializedTestCases.Add(testCase);

                    suggestion.ReviewStatus = isModify
                        ? ReviewStatus.ModifiedAndApproved
                        : ReviewStatus.Approved;
                    suggestion.ReviewedById = currentUserId;
                    suggestion.ReviewedAt = now;
                    suggestion.ReviewNotes = approvalItem.ReviewNotes;
                    suggestion.AppliedTestCaseId = testCase.Id;
                    suggestion.UpdatedDateTime = now;
                    suggestion.RowVersion = Guid.NewGuid().ToByteArray();

                    await _testCaseRepository.AddAsync(testCase, ct);
                    await _requestRepository.AddAsync(testCase.Request, ct);
                    await _expectationRepository.AddAsync(testCase.Expectation, ct);

                    foreach (var variable in testCase.Variables)
                    {
                        await _variableRepository.AddAsync(variable, ct);
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
                            SuggestionId = suggestion.Id,
                        }, JsonOpts),
                        ChangeReason = BuildChangeReason(suggestion.Id, isModify, approvalItems.Count > 1),
                        VersionAfterChange = 1,
                        CreatedDateTime = now,
                    }, ct);

                    await _suggestionRepository.UpdateAsync(suggestion, ct);
                }

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
                suite.LastModifiedById = currentUserId;
                suite.UpdatedDateTime = now;
                suite.RowVersion = Guid.NewGuid().ToByteArray();
                await _suiteRepository.UpdateAsync(suite, ct);

                await _testCaseRepository.UnitOfWork.SaveChangesAsync(ct);
            }, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (_suggestionRepository.IsDbUpdateConcurrencyException(ex))
        {
            throw new ConflictException(
                "CONCURRENCY_CONFLICT",
                "Suggestion đã được thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
        }

        await _subscriptionLimitService.IncrementUsageAsync(
            new IncrementUsageRequest
            {
                UserId = currentUserId,
                LimitType = LimitType.MaxTestCasesPerSuite,
                IncrementValue = approvalItems.Count,
            },
            cancellationToken);

        _logger.LogInformation(
            "Approved {SuggestionCount} LLM suggestion(s). SuggestionIds={SuggestionIds}, TestSuiteId={TestSuiteId}, ActorUserId={ActorUserId}",
            approvalItems.Count,
            approvalItems.Select(x => x.Suggestion.Id).ToArray(),
            suite.Id,
            currentUserId);

        return new LlmSuggestionApprovalBatchResult
        {
            Suggestions = approvalItems.Select(x => x.Suggestion).ToList(),
            TestCases = materializedTestCases,
        };
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
}
