using ClassifiedAds.CrossCuttingConcerns.DateTimes;
using ClassifiedAds.Modules.Notification.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Notification.EmailQueue;

/// <summary>
/// Periodic sweep that finds unsent emails in the database that are NOT yet
/// in the in-memory channel (e.g. after app restart, or if the enqueue failed).
/// This guarantees at-least-once delivery even if the process crashes.
/// </summary>
public sealed class EmailDbSweepWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEmailQueueWriter _writer;
    private readonly ILogger<EmailDbSweepWorker> _logger;
    private readonly EmailQueueOptions _options;

    public EmailDbSweepWorker(
        IServiceProvider serviceProvider,
        IEmailQueueWriter writer,
        ILogger<EmailDbSweepWorker> logger,
        IOptions<EmailQueueOptions> options)
    {
        _serviceProvider = serviceProvider;
        _writer = writer;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EmailDbSweepWorker started. Interval={Interval}s", _options.DbSweepIntervalSeconds);

        // Initial delay to let the app finish starting
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                _logger.LogDebug("EmailDbSweepWorker stopping because service provider has been disposed.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EmailDbSweepWorker encountered an error.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.DbSweepIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEmailMessageRepository>();
        var dateTime = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var now = dateTime.OffsetUtcNow;

        // Find unsent, non-dead-letter, eligible-for-retry emails
        var orphanedEmails = repository.GetQueryableSet()
            .Where(x => x.SentDateTime == null)
            .Where(x => x.ExpiredDateTime == null || x.ExpiredDateTime > now) // not dead-lettered
            .Where(x => x.MaxAttemptCount == 0 || x.AttemptCount < x.MaxAttemptCount)
            .Where(x => x.NextAttemptDateTime == null || x.NextAttemptDateTime <= now)
            .Select(x => new { x.Id, x.AttemptCount })
            .ToList();

        if (orphanedEmails.Count == 0)
        {
            return;
        }

        _logger.LogInformation("DB sweep found {Count} unsent email(s) to enqueue.", orphanedEmails.Count);

        foreach (var e in orphanedEmails)
        {
            await _writer.EnqueueAsync(new EmailQueueItem
            {
                EmailMessageId = e.Id,
                Attempt = e.AttemptCount + 1,
                EnqueuedAtUtc = DateTimeOffset.UtcNow,
            }, ct);
        }
    }
}
