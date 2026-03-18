using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class ReviewLlmSuggestionCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid SuggestionId { get; set; }
    public Guid CurrentUserId { get; set; }
    public string ReviewAction { get; set; }
    public string RowVersion { get; set; }
    public string ReviewNotes { get; set; }
    public EditableLlmSuggestionInput ModifiedContent { get; set; }
    public LlmSuggestionModel Result { get; set; }
}

public class ReviewLlmSuggestionCommandHandler : ICommandHandler<ReviewLlmSuggestionCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;
    private readonly ILlmSuggestionReviewService _reviewService;
    private readonly ILogger<ReviewLlmSuggestionCommandHandler> _logger;

    public ReviewLlmSuggestionCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository,
        ILlmSuggestionReviewService reviewService,
        ILogger<ReviewLlmSuggestionCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
        _reviewService = reviewService;
        _logger = logger;
    }

    public async Task HandleAsync(
        ReviewLlmSuggestionCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidationException.Requires(command.SuggestionId != Guid.Empty, "SuggestionId là bắt buộc.");
        ValidationException.Requires(command.TestSuiteId != Guid.Empty, "TestSuiteId là bắt buộc.");
        ValidationException.Requires(command.CurrentUserId != Guid.Empty, "CurrentUserId là bắt buộc.");
        ValidationException.Requires(
            !string.IsNullOrWhiteSpace(command.RowVersion),
            "RowVersion la bat buoc cho concurrency control.");

        var validActions = new[] { "Approve", "Reject", "Modify" };
        ValidationException.Requires(
            validActions.Contains(command.ReviewAction, StringComparer.OrdinalIgnoreCase),
            "ReviewAction phai la 'Approve', 'Reject', hoac 'Modify'.");

        var isReject = string.Equals(command.ReviewAction, "Reject", StringComparison.OrdinalIgnoreCase);
        var isModify = string.Equals(command.ReviewAction, "Modify", StringComparison.OrdinalIgnoreCase);

        if (isReject)
        {
            ValidationException.Requires(
                !string.IsNullOrWhiteSpace(command.ReviewNotes),
                "ReviewNotes la bat buoc khi reject suggestion.");
        }

        if (isModify)
        {
            ValidationException.Requires(
                command.ModifiedContent != null,
                "ModifiedContent là bắt buộc khi Modify suggestion.");
        }

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");
        }

        ValidationException.Requires(
            suite.CreatedById == command.CurrentUserId,
            "Ban khong phai chu so huu cua test suite nay.");

        var suggestion = await _suggestionRepository.FirstOrDefaultAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.Id == command.SuggestionId && x.TestSuiteId == command.TestSuiteId));

        if (suggestion == null)
        {
            throw new NotFoundException($"Không tìm thấy suggestion với mã '{command.SuggestionId}'.");
        }

        if (IsIdempotentApproveRequest(command.ReviewAction, suggestion))
        {
            command.Result = LlmSuggestionModel.FromEntity(suggestion);

            _logger.LogInformation(
                "LLM suggestion approve request was idempotent. SuggestionId={SuggestionId}, AppliedTestCaseId={AppliedTestCaseId}, TestSuiteId={TestSuiteId}, ActorUserId={ActorUserId}",
                suggestion.Id,
                suggestion.AppliedTestCaseId,
                command.TestSuiteId,
                command.CurrentUserId);

            return;
        }

        ValidationException.Requires(
            suggestion.ReviewStatus == ReviewStatus.Pending,
            $"Không thể review suggestion ở trạng thái '{suggestion.ReviewStatus}'. Chỉ có thể review suggestion đang Pending.");

        byte[] parsedRowVersion;
        try
        {
            parsedRowVersion = Convert.FromBase64String(command.RowVersion);
        }
        catch (FormatException)
        {
            throw new ValidationException("RowVersion không hợp lệ. Giá trị phải là chuỗi Base64.");
        }

        _suggestionRepository.SetRowVersion(suggestion, parsedRowVersion);

        if (isReject)
        {
            await _reviewService.RejectAsync(
                suggestion,
                command.CurrentUserId,
                command.ReviewNotes,
                cancellationToken);
        }
        else
        {
            var singleApproval = new LlmSuggestionApprovalItem
            {
                Suggestion = suggestion,
                ReviewNotes = command.ReviewNotes,
                ModifiedContent = isModify ? command.ModifiedContent : null,
            };

            await _reviewService.ApproveManyAsync(
                suite,
                new[] { singleApproval },
                command.CurrentUserId,
                cancellationToken);
        }

        command.Result = LlmSuggestionModel.FromEntity(suggestion);

        _logger.LogInformation(
            "LLM suggestion reviewed. SuggestionId={SuggestionId}, ReviewAction={ReviewAction}, TestSuiteId={TestSuiteId}, ActorUserId={ActorUserId}",
            suggestion.Id,
            command.ReviewAction,
            command.TestSuiteId,
            command.CurrentUserId);
    }

    private static bool IsIdempotentApproveRequest(string reviewAction, LlmSuggestion suggestion)
    {
        return string.Equals(reviewAction, "Approve", StringComparison.OrdinalIgnoreCase)
            && suggestion.AppliedTestCaseId.HasValue
            && (suggestion.ReviewStatus == ReviewStatus.Approved
                || suggestion.ReviewStatus == ReviewStatus.ModifiedAndApproved);
    }
}
