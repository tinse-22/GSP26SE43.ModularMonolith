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
        ValidationException.Requires(command.SuggestionId != Guid.Empty, "SuggestionId la bat buoc.");
        ValidationException.Requires(command.TestSuiteId != Guid.Empty, "TestSuiteId la bat buoc.");
        ValidationException.Requires(
            !string.IsNullOrWhiteSpace(command.RowVersion),
            "RowVersion la bat buoc cho concurrency control.");

        var validActions = new[] { "Approve", "Reject", "Modify" };
        ValidationException.Requires(
            validActions.Contains(command.ReviewAction, StringComparer.OrdinalIgnoreCase),
            "ReviewAction phai la 'Approve', 'Reject', hoac 'Modify'.");

        if (string.Equals(command.ReviewAction, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            ValidationException.Requires(
                !string.IsNullOrWhiteSpace(command.ReviewNotes),
                "ReviewNotes la bat buoc khi reject suggestion.");
        }

        if (string.Equals(command.ReviewAction, "Modify", StringComparison.OrdinalIgnoreCase))
        {
            ValidationException.Requires(
                command.ModifiedContent != null,
                "ModifiedContent la bat buoc khi Modify suggestion.");
            ValidationException.Requires(
                !string.IsNullOrWhiteSpace(command.ModifiedContent?.Name),
                "ModifiedContent.Name la bat buoc.");
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

        var suggestion = await _suggestionRepository.FirstOrDefaultAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.Id == command.SuggestionId && x.TestSuiteId == command.TestSuiteId));

        if (suggestion == null)
        {
            throw new NotFoundException($"Khong tim thay suggestion voi ma '{command.SuggestionId}'.");
        }

        ValidationException.Requires(
            suggestion.ReviewStatus == ReviewStatus.Pending,
            $"Khong the review suggestion o trang thai '{suggestion.ReviewStatus}'. Chi co the review suggestion dang Pending.");

        var parsedRowVersion = Convert.FromBase64String(command.RowVersion);
        _suggestionRepository.SetRowVersion(suggestion, parsedRowVersion);

        if (string.Equals(command.ReviewAction, "Reject", StringComparison.OrdinalIgnoreCase))
        {
            await _reviewService.RejectAsync(
                suggestion,
                command.CurrentUserId,
                command.ReviewNotes,
                cancellationToken);
        }
        else
        {
            var isModify = string.Equals(command.ReviewAction, "Modify", StringComparison.OrdinalIgnoreCase);

            await _reviewService.ApproveManyAsync(
                suite,
                new[]
                {
                    new LlmSuggestionApprovalItem
                    {
                        Suggestion = suggestion,
                        ReviewNotes = command.ReviewNotes,
                        ModifiedContent = isModify ? command.ModifiedContent : null,
                    },
                },
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
}
