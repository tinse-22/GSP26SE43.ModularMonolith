using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public interface ILlmSuggestionFeedbackUpsertService
{
    Task<LlmSuggestionFeedbackUpsertResult> UpsertAsync(
        LlmSuggestionFeedbackUpsertRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class LlmSuggestionFeedbackUpsertRequest
{
    public Guid TestSuiteId { get; init; }

    public Guid SuggestionId { get; init; }

    public Guid CurrentUserId { get; init; }

    public LlmSuggestionFeedbackSignal Signal { get; init; }

    public string Notes { get; init; }
}

public sealed class LlmSuggestionFeedbackUpsertResult
{
    public LlmSuggestionFeedback Feedback { get; init; }

    public bool WasUpdate { get; init; }
}

public class LlmSuggestionFeedbackUpsertService : ILlmSuggestionFeedbackUpsertService
{
    private readonly TestGenerationDbContext _dbContext;

    public LlmSuggestionFeedbackUpsertService(TestGenerationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<LlmSuggestionFeedbackUpsertResult> UpsertAsync(
        LlmSuggestionFeedbackUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_dbContext.Database.IsRelational())
        {
            return await UpsertCoreAsync(request, cancellationToken);
        }

        return await _dbContext.ExecuteInTransactionAsync(
            ct => UpsertCoreAsync(request, ct),
            cancellationToken: cancellationToken);
    }

    private async Task<LlmSuggestionFeedbackUpsertResult> UpsertCoreAsync(
        LlmSuggestionFeedbackUpsertRequest request,
        CancellationToken cancellationToken)
    {
        var suite = await LoadSuiteForWriteAsync(request.TestSuiteId, cancellationToken);
        if (suite == null)
        {
            throw new NotFoundException($"Khong tim thay test suite voi ma '{request.TestSuiteId}'.");
        }

        ValidationException.Requires(
            suite.CreatedById == request.CurrentUserId,
            "Ban khong phai chu so huu cua test suite nay.");
        ValidationException.Requires(
            suite.Status != TestSuiteStatus.Archived,
            "Khong the feedback suggestion cho test suite da archived.");

        var suggestion = await LoadSuggestionForWriteAsync(
            request.TestSuiteId,
            request.SuggestionId,
            cancellationToken);
        if (suggestion == null)
        {
            throw new NotFoundException($"Khong tim thay suggestion voi ma '{request.SuggestionId}'.");
        }

        ValidationException.Requires(
            suggestion.ReviewStatus != ReviewStatus.Superseded,
            "Khong the feedback suggestion da superseded.");

        var feedback = await _dbContext.LlmSuggestionFeedbacks
            .SingleOrDefaultAsync(
                x => x.TestSuiteId == request.TestSuiteId &&
                    x.SuggestionId == request.SuggestionId &&
                    x.UserId == request.CurrentUserId,
                cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var isUpdate = feedback != null;

        if (feedback == null)
        {
            feedback = new LlmSuggestionFeedback
            {
                Id = Guid.NewGuid(),
                SuggestionId = suggestion.Id,
                TestSuiteId = suggestion.TestSuiteId,
                EndpointId = suggestion.EndpointId,
                UserId = request.CurrentUserId,
                FeedbackSignal = request.Signal,
                Notes = request.Notes,
                CreatedDateTime = now,
                RowVersion = Guid.NewGuid().ToByteArray(),
            };

            await _dbContext.LlmSuggestionFeedbacks.AddAsync(feedback, cancellationToken);
        }
        else
        {
            feedback.TestSuiteId = suggestion.TestSuiteId;
            feedback.EndpointId = suggestion.EndpointId;
            feedback.FeedbackSignal = request.Signal;
            feedback.Notes = request.Notes;
            feedback.UpdatedDateTime = now;
            feedback.RowVersion = Guid.NewGuid().ToByteArray();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new LlmSuggestionFeedbackUpsertResult
        {
            Feedback = feedback,
            WasUpdate = isUpdate,
        };
    }

    private Task<TestSuite> LoadSuiteForWriteAsync(Guid testSuiteId, CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsRelational())
        {
            return _dbContext.TestSuites
                .SingleOrDefaultAsync(x => x.Id == testSuiteId, cancellationToken);
        }

        DetachTrackedSuite(testSuiteId);

        return _dbContext.TestSuites
            .FromSqlInterpolated($@"
SELECT *
FROM testgen.""TestSuites""
WHERE ""Id"" = {testSuiteId}
FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private Task<LlmSuggestion> LoadSuggestionForWriteAsync(
        Guid testSuiteId,
        Guid suggestionId,
        CancellationToken cancellationToken)
    {
        if (!_dbContext.Database.IsRelational())
        {
            return _dbContext.LlmSuggestions
                .SingleOrDefaultAsync(
                    x => x.Id == suggestionId && x.TestSuiteId == testSuiteId,
                    cancellationToken);
        }

        DetachTrackedSuggestion(suggestionId);

        return _dbContext.LlmSuggestions
            .FromSqlInterpolated($@"
SELECT *
FROM testgen.""LlmSuggestions""
WHERE ""Id"" = {suggestionId}
    AND ""TestSuiteId"" = {testSuiteId}
FOR UPDATE")
            .AsTracking()
            .SingleOrDefaultAsync(cancellationToken);
    }

    private void DetachTrackedSuite(Guid testSuiteId)
    {
        var trackedEntry = _dbContext.ChangeTracker.Entries<TestSuite>()
            .FirstOrDefault(x => x.Entity.Id == testSuiteId);

        if (trackedEntry != null)
        {
            trackedEntry.State = EntityState.Detached;
        }
    }

    private void DetachTrackedSuggestion(Guid suggestionId)
    {
        var trackedEntry = _dbContext.ChangeTracker.Entries<LlmSuggestion>()
            .FirstOrDefault(x => x.Entity.Id == suggestionId);

        if (trackedEntry != null)
        {
            trackedEntry.State = EntityState.Detached;
        }
    }
}
