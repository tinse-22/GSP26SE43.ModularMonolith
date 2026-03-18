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

public class UpsertLlmSuggestionFeedbackCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid SuggestionId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string Signal { get; set; }

    public string Notes { get; set; }

    public LlmSuggestionFeedbackModel Result { get; set; }
}

public class UpsertLlmSuggestionFeedbackCommandHandler : ICommandHandler<UpsertLlmSuggestionFeedbackCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;
    private readonly ILlmSuggestionFeedbackUpsertService _feedbackUpsertService;
    private readonly LlmSuggestionFeedbackMetrics _metrics;
    private readonly ILogger<UpsertLlmSuggestionFeedbackCommandHandler> _logger;

    public UpsertLlmSuggestionFeedbackCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository,
        ILlmSuggestionFeedbackUpsertService feedbackUpsertService,
        LlmSuggestionFeedbackMetrics metrics,
        ILogger<UpsertLlmSuggestionFeedbackCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
        _feedbackUpsertService = feedbackUpsertService;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task HandleAsync(
        UpsertLlmSuggestionFeedbackCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidationException.Requires(command.TestSuiteId != Guid.Empty, "TestSuiteId la bat buoc.");
        ValidationException.Requires(command.SuggestionId != Guid.Empty, "SuggestionId la bat buoc.");
        ValidationException.Requires(command.CurrentUserId != Guid.Empty, "CurrentUserId la bat buoc.");
        ValidationException.Requires(
            TryParseSignal(command.Signal, out var signal),
            "Signal phai la 'Helpful' hoac 'NotHelpful'.");

        var normalizedNotes = LlmSuggestionFeedbackTextSanitizer.NormalizeForStorage(command.Notes);
        ValidationException.Requires(
            normalizedNotes == null || normalizedNotes.Length <= LlmSuggestionFeedbackTextSanitizer.MaxNotesLength,
            $"Notes khong duoc vuot qua {LlmSuggestionFeedbackTextSanitizer.MaxNotesLength} ky tu.");

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
        ValidationException.Requires(
            suite.Status != TestSuiteStatus.Archived,
            "Khong the feedback suggestion cho test suite da archived.");

        var suggestion = await _suggestionRepository.FirstOrDefaultAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.Id == command.SuggestionId && x.TestSuiteId == command.TestSuiteId));

        if (suggestion == null)
        {
            throw new NotFoundException($"Khong tim thay suggestion voi ma '{command.SuggestionId}'.");
        }

        ValidationException.Requires(
            suggestion.ReviewStatus != ReviewStatus.Superseded,
            "Khong the feedback suggestion da superseded.");

        var upsertResult = await _feedbackUpsertService.UpsertAsync(
            new LlmSuggestionFeedbackUpsertRequest
            {
                TestSuiteId = command.TestSuiteId,
                SuggestionId = command.SuggestionId,
                CurrentUserId = command.CurrentUserId,
                Signal = signal,
                Notes = normalizedNotes,
            },
            cancellationToken);

        _metrics.RecordUpsert(signal);
        command.Result = LlmSuggestionFeedbackModel.FromEntity(upsertResult.Feedback);

        _logger.LogInformation(
            "Upserted LLM suggestion feedback. TestSuiteId={TestSuiteId}, SuggestionId={SuggestionId}, EndpointId={EndpointId}, FeedbackSignal={FeedbackSignal}, FeedbackUpdated={FeedbackUpdated}, ActorUserId={ActorUserId}",
            command.TestSuiteId,
            command.SuggestionId,
            upsertResult.Feedback.EndpointId,
            signal,
            upsertResult.WasUpdate,
            command.CurrentUserId);
    }

    private static bool TryParseSignal(string rawSignal, out LlmSuggestionFeedbackSignal signal)
    {
        signal = default;
        if (string.IsNullOrWhiteSpace(rawSignal))
        {
            return false;
        }

        var normalizedSignal = rawSignal.Trim();
        if (string.Equals(normalizedSignal, nameof(LlmSuggestionFeedbackSignal.Helpful), StringComparison.OrdinalIgnoreCase))
        {
            signal = LlmSuggestionFeedbackSignal.Helpful;
            return true;
        }

        if (string.Equals(normalizedSignal, nameof(LlmSuggestionFeedbackSignal.NotHelpful), StringComparison.OrdinalIgnoreCase))
        {
            signal = LlmSuggestionFeedbackSignal.NotHelpful;
            return true;
        }

        return false;
    }
}
