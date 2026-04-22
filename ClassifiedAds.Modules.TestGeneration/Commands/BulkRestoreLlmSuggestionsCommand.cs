using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class BulkRestoreLlmSuggestionsCommand : ICommand
{
    public Guid TestSuiteId { get; set; }
    public Guid CurrentUserId { get; set; }
    public List<Guid> SuggestionIds { get; set; }
    public BulkOperationResultModel Result { get; set; }
}

public class BulkRestoreLlmSuggestionsCommandHandler : ICommandHandler<BulkRestoreLlmSuggestionsCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<LlmSuggestion, Guid> _suggestionRepository;

    public BulkRestoreLlmSuggestionsCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<LlmSuggestion, Guid> suggestionRepository)
    {
        _suiteRepository = suiteRepository;
        _suggestionRepository = suggestionRepository;
    }

    public async Task HandleAsync(BulkRestoreLlmSuggestionsCommand command, CancellationToken cancellationToken = default)
    {
        // 1) Validate
        ValidationException.Requires(command.TestSuiteId != Guid.Empty, "TestSuiteId là bắt buộc.");
        ValidationException.Requires(command.CurrentUserId != Guid.Empty, "CurrentUserId là bắt buộc.");
        ValidationException.Requires(
            command.SuggestionIds != null && command.SuggestionIds.Count > 0,
            "SuggestionIds là bắt buộc và phải có ít nhất 1 phần tử.");

        // 2) Load and verify suite
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet()
                .Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");
        }

        ValidationException.Requires(
            suite.CreatedById == command.CurrentUserId,
            "Bạn không phải chủ sở hữu của test suite này.");

        // 3) Load suggestions by IDs
        var distinctIds = command.SuggestionIds.Distinct().ToList();
        var suggestions = await _suggestionRepository.ToListAsync(
            _suggestionRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId && distinctIds.Contains(x.Id)));

        var now = DateTimeOffset.UtcNow;
        var processedIds = new List<Guid>();
        var skippedIds = new List<Guid>();

        foreach (var s in suggestions)
        {
            // Case A: soft-deleted -> restore soft-delete fields
            if (s.IsDeleted)
            {
                s.IsDeleted = false;
                s.DeletedAt = null;
                s.DeletedById = null;
                s.UpdatedDateTime = now;
                s.RowVersion = Guid.NewGuid().ToByteArray();
                await _suggestionRepository.UpdateAsync(s, cancellationToken);

                processedIds.Add(s.Id);
                continue;
            }

            // Case B: not deleted but review-state is Superseded or Rejected -> move back to Pending
            if (s.ReviewStatus == ReviewStatus.Superseded || s.ReviewStatus == ReviewStatus.Rejected)
            {
                s.ReviewStatus = ReviewStatus.Pending;
                s.ReviewedById = null;
                s.ReviewedAt = null;
                s.ReviewNotes = null;
                s.UpdatedDateTime = now;
                s.RowVersion = Guid.NewGuid().ToByteArray();
                await _suggestionRepository.UpdateAsync(s, cancellationToken);

                processedIds.Add(s.Id);
                continue;
            }

            // Otherwise skip (neither deleted nor eligible for un-supersede/un-reject)
            skippedIds.Add(s.Id);
        }

        // IDs not found
        var notFoundIds = distinctIds.Except(suggestions.Select(x => x.Id)).ToList();
        skippedIds.AddRange(notFoundIds);

        await _suggestionRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = new BulkOperationResultModel
        {
            TestSuiteId = command.TestSuiteId,
            Operation = "Restore",
            EntityType = "LlmSuggestion",
            RequestedCount = distinctIds.Count,
            ProcessedCount = processedIds.Count,
            SkippedCount = skippedIds.Count,
            ProcessedIds = processedIds,
            SkippedIds = skippedIds,
            SkipReason = skippedIds.Count > 0 ? "Chưa bị xóa, không phải Superseded/Rejected, hoặc không tìm thấy." : null,
            OperatedAt = now,
        };
    }
}
