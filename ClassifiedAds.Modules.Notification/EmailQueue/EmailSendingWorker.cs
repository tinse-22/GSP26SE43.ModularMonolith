using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Infrastructure.Notification.Email;
using ClassifiedAds.Modules.Notification.Entities;
using ClassifiedAds.Modules.Notification.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Notification.EmailQueue;

/// <summary>
/// Long-running BackgroundService that consumes emails from <see cref="EmailChannel"/>
/// and sends them via <see cref="IEmailNotification"/> with full Polly resilience pipeline
/// (retry + exponential backoff, circuit breaker, per-call timeout).
/// 
/// On permanent failure the email is moved to dead-letter status in the database.
/// </summary>
public sealed class EmailSendingWorker : BackgroundService
{
    private readonly IEmailQueueReader _reader;
    private readonly IEmailQueueWriter _writer;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EmailSendingWorker> _logger;
    private readonly EmailQueueOptions _options;
    private readonly ResiliencePipeline _resiliencePipeline;

    // Metrics counters (simple; swap for System.Diagnostics.Metrics in production)
    private long _sentCount;
    private long _failedCount;
    private long _deadLetterCount;

    public EmailSendingWorker(
        IEmailQueueReader reader,
        IEmailQueueWriter writer,
        IServiceProvider serviceProvider,
        ILogger<EmailSendingWorker> logger,
        IOptions<EmailQueueOptions> options)
    {
        _reader = reader;
        _writer = writer;
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _resiliencePipeline = BuildResiliencePipeline();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "EmailSendingWorker started. Parallelism={Parallelism}, MaxRetry={MaxRetry}, CircuitBreaker={CB}",
            _options.MaxDegreeOfParallelism, _options.MaxRetryAttempts, _options.CircuitBreakerFailureThreshold);

        // Use SemaphoreSlim to limit concurrency (simple, no TPL Dataflow needed for ≤10 emails)
        using var semaphore = new SemaphoreSlim(_options.MaxDegreeOfParallelism);

        try
        {
            await foreach (var item in _reader.ReadAllAsync(stoppingToken))
            {
                await semaphore.WaitAsync(stoppingToken);

                // Fire-and-forget inside semaphore; exceptions handled internally
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await ProcessEmailAsync(item, stoppingToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("EmailSendingWorker cancelled.");
        }
    }

    private async Task ProcessEmailAsync(EmailQueueItem item, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IEmailMessageRepository>();
            var emailNotification = scope.ServiceProvider.GetRequiredService<IEmailNotification>();
            var dateTime = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            // ─── Idempotency check ───────────────────────────────────────
            var email = await repository.FirstOrDefaultAsync(repository.GetQueryableSet()
                .Where(x => x.Id == item.EmailMessageId));

            if (email == null)
            {
                _logger.LogWarning("Email {EmailId} not found in DB — skipping (already deleted?).", item.EmailMessageId);
                return;
            }

            if (email.SentDateTime != null)
            {
                _logger.LogDebug("Email {EmailId} already sent at {SentAt} — idempotent skip.", item.EmailMessageId, email.SentDateTime);
                return;
            }

            // ─── Send with resilience pipeline ───────────────────────────
            string log = Environment.NewLine + Environment.NewLine
                + $"[{dateTime.OffsetNow.ToString(CultureInfo.InvariantCulture)}] Attempt {item.Attempt}: ";

            try
            {
                await _resiliencePipeline.ExecuteAsync(async token =>
                {
                    await emailNotification.SendAsync(new DTOs.EmailMessageDTO
                    {
                        From = email.From,
                        Tos = email.Tos,
                        CCs = email.CCs,
                        BCCs = email.BCCs,
                        Subject = email.Subject,
                        Body = email.Body,
                    }, token);
                }, ct);

                // ─── Success ─────────────────────────────────────────────
                email.SentDateTime = dateTime.OffsetUtcNow;
                email.AttemptCount = item.Attempt;
                email.Log = (email.Log ?? string.Empty) + log + "Sent successfully.";
                email.Log = email.Log.Trim();
                email.UpdatedDateTime = dateTime.OffsetUtcNow;
                await repository.UnitOfWork.SaveChangesAsync(ct);

                Interlocked.Increment(ref _sentCount);
                _logger.LogInformation(
                    "Email {EmailId} sent in {Elapsed}ms (attempt {Attempt}). Total sent: {Total}",
                    item.EmailMessageId, sw.ElapsedMilliseconds, item.Attempt, _sentCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Interlocked.Increment(ref _failedCount);

                email.AttemptCount = item.Attempt;
                email.Log = (email.Log ?? string.Empty) + log + ex.ToString();
                email.Log = email.Log.Trim();
                email.UpdatedDateTime = dateTime.OffsetUtcNow;

                if (item.Attempt >= _options.MaxRetryAttempts)
                {
                    // ─── Dead-letter ─────────────────────────────────────
                    email.ExpiredDateTime = dateTime.OffsetUtcNow; // mark as dead-letter
                    email.Log += Environment.NewLine + "[DEAD-LETTER] Max retry attempts exhausted.";
                    await repository.UnitOfWork.SaveChangesAsync(ct);

                    Interlocked.Increment(ref _deadLetterCount);
                    _logger.LogError(ex,
                        "Email {EmailId} moved to DEAD-LETTER after {Attempts} attempts. Total DL: {DL}",
                        item.EmailMessageId, item.Attempt, _deadLetterCount);
                }
                else
                {
                    // ─── Re-enqueue with backoff ─────────────────────────
                    var backoff = CalculateBackoff(item.Attempt);
                    email.NextAttemptDateTime = dateTime.OffsetUtcNow + backoff;

                    if (email.MaxAttemptCount == 0)
                    {
                        email.MaxAttemptCount = _options.MaxRetryAttempts;
                    }

                    await repository.UnitOfWork.SaveChangesAsync(ct);

                    _logger.LogWarning(ex,
                        "Email {EmailId} failed (attempt {Attempt}/{Max}). Re-enqueue after {Delay}s.",
                        item.EmailMessageId, item.Attempt, _options.MaxRetryAttempts, backoff.TotalSeconds);

                    // Delay then re-enqueue
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(backoff, ct);
                            await _writer.EnqueueAsync(new EmailQueueItem
                            {
                                EmailMessageId = item.EmailMessageId,
                                Attempt = item.Attempt + 1,
                                EnqueuedAtUtc = item.EnqueuedAtUtc,
                            }, ct);
                        }
                        catch (OperationCanceledException)
                        {
                            // Shutting down, DB sweep will pick it up on next restart
                        }
                    }, ct);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Processing of email {EmailId} was cancelled.", item.EmailMessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error processing email {EmailId}.", item.EmailMessageId);
        }
    }

    private TimeSpan CalculateBackoff(int attempt)
    {
        // Exponential backoff: base * 2^(attempt-1), capped
        var delay = TimeSpan.FromSeconds(_options.BaseDelaySeconds * Math.Pow(2, attempt - 1));
        var max = TimeSpan.FromSeconds(_options.MaxDelaySeconds);
        return delay > max ? max : delay;
    }

    private ResiliencePipeline BuildResiliencePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(_options.SendTimeoutSeconds),
                OnTimeout = args =>
                {
                    _logger.LogWarning("Email send timed out after {Timeout}s.", _options.SendTimeoutSeconds);
                    return default;
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = 1.0, // open after N consecutive failures
                MinimumThroughput = _options.CircuitBreakerFailureThreshold,
                SamplingDuration = TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds * 2),
                BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning("Email circuit breaker OPENED for {Duration}s.", _options.CircuitBreakerDurationSeconds);
                    return default;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Email circuit breaker CLOSED — service recovered.");
                    return default;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("Email circuit breaker HALF-OPEN — probing.");
                    return default;
                },
            })
            .Build();
    }
}
