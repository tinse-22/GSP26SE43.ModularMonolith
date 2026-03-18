using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class BulkReviewLlmSuggestionsCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string Action { get; set; }

    public string ReviewNotes { get; set; }

    public string FilterBySuggestionType { get; set; }

    public string FilterByTestType { get; set; }

    public Guid? FilterByEndpointId { get; set; }

    public BulkReviewLlmSuggestionsResultModel Result { get; set; }
}

public class BulkReviewLlmSuggestionsCommandHandler : ICommandHandler<BulkReviewLlmSuggestionsCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;
    private readonly ILlmSuggestionReviewService _reviewService;
    private readonly ILogger<BulkReviewLlmSuggestionsCommandHandler> _logger;

    public BulkReviewLlmSuggestionsCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository,
        ILlmSuggestionReviewService reviewService,
        ILogger<BulkReviewLlmSuggestionsCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
        _reviewService = reviewService;
        _logger = logger;
    }

    public async Task HandleAsync(
        BulkReviewLlmSuggestionsCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidationException.Requires(command.TestSuiteId != Guid.Empty, "TestSuiteId la bat buoc.");
        ValidationException.Requires(command.CurrentUserId != Guid.Empty, "CurrentUserId la bat buoc.");

        var isApprove = string.Equals(command.Action, "Approve", StringComparison.OrdinalIgnoreCase);
        var isReject = string.Equals(command.Action, "Reject", StringComparison.OrdinalIgnoreCase);
        var action = isApprove ? "Approve" : "Reject";
        ValidationException.Requires(isApprove || isReject, "Action phai la 'Approve' hoac 'Reject'.");

        if (isReject)
        {
            ValidationException.Requires(
                !string.IsNullOrWhiteSpace(command.ReviewNotes),
                "ReviewNotes la bat buoc khi bulk reject suggestions.");
        }

        LlmSuggestionType? filterBySuggestionType = null;
        if (!string.IsNullOrWhiteSpace(command.FilterBySuggestionType))
        {
            ValidationException.Requires(
                Enum.TryParse<LlmSuggestionType>(command.FilterBySuggestionType, true, out var parsedSuggestionType),
                "FilterBySuggestionType khong hop le.");
            filterBySuggestionType = parsedSuggestionType;
        }

        TestType? filterByTestType = null;
        if (!string.IsNullOrWhiteSpace(command.FilterByTestType))
        {
            ValidationException.Requires(
                Enum.TryParse<TestType>(command.FilterByTestType, true, out var parsedTestType),
                "FilterByTestType khong hop le.");
            filterByTestType = parsedTestType;
        }

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Khong tim thay test suite voi ma '{command.TestSuiteId}'.");
        }

        ValidationException.Requires(
            suite.CreatedById == command.CurrentUserId,
            "Ban khong phai chu so huu cua test suite nay.");

        var queryable = _suggestionRepository.GetQueryableSet()
            .Where(x => x.TestSuiteId == command.TestSuiteId
                && x.ReviewStatus == ReviewStatus.Pending);

        if (filterBySuggestionType.HasValue)
        {
            queryable = queryable.Where(x => x.SuggestionType == filterBySuggestionType.Value);
        }

        if (filterByTestType.HasValue)
        {
            queryable = queryable.Where(x => x.TestType == filterByTestType.Value);
        }

        if (command.FilterByEndpointId.HasValue)
        {
            queryable = queryable.Where(x => x.EndpointId == command.FilterByEndpointId.Value);
        }

        var suggestions = await _suggestionRepository.ToListAsync(
            queryable.OrderBy(x => x.DisplayOrder));

        var now = DateTimeOffset.UtcNow;
        if (suggestions.Count == 0)
        {
            command.Result = new BulkReviewLlmSuggestionsResultModel
            {
                TestSuiteId = command.TestSuiteId,
                Action = action,
                MatchedCount = 0,
                ProcessedCount = 0,
                MaterializedCount = 0,
                ReviewedAt = now,
            };
            return;
        }

        var appliedTestCaseIds = Array.Empty<Guid>();

        if (isApprove)
        {
            var approvalResult = await _reviewService.ApproveManyAsync(
                suite,
                suggestions.Select(x => new LlmSuggestionApprovalItem
                {
                    Suggestion = x,
                    ReviewNotes = command.ReviewNotes,
                }).ToList(),
                command.CurrentUserId,
                cancellationToken);

            appliedTestCaseIds = approvalResult.TestCases.Select(x => x.Id).ToArray();
        }
        else
        {
            await _reviewService.RejectManyAsync(
                suggestions,
                command.CurrentUserId,
                command.ReviewNotes,
                cancellationToken);
        }

        var reviewedAt = suggestions
            .Select(x => x.ReviewedAt)
            .Where(x => x.HasValue)
            .Select(x => x.Value)
            .DefaultIfEmpty(DateTimeOffset.UtcNow)
            .Max();

        command.Result = new BulkReviewLlmSuggestionsResultModel
        {
            TestSuiteId = command.TestSuiteId,
            Action = action,
            MatchedCount = suggestions.Count,
            ProcessedCount = suggestions.Count,
            MaterializedCount = appliedTestCaseIds.Length,
            ReviewedAt = reviewedAt,
            SuggestionIds = suggestions.Select(x => x.Id).ToList(),
            AppliedTestCaseIds = appliedTestCaseIds.ToList(),
        };

        _logger.LogInformation(
            "Bulk reviewed LLM suggestions. TestSuiteId={TestSuiteId}, Action={Action}, ProcessedCount={ProcessedCount}, MaterializedCount={MaterializedCount}, ActorUserId={ActorUserId}",
            command.TestSuiteId,
            command.Result.Action,
            command.Result.ProcessedCount,
            command.Result.MaterializedCount,
            command.CurrentUserId);
    }
}
