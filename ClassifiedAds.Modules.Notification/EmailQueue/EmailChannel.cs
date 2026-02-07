using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Notification.EmailQueue;

/// <summary>
/// Thread-safe, bounded, in-memory Channel that acts as the producer/consumer queue.
/// Producers call <see cref="EnqueueAsync"/> (non-blocking unless channel is full).
/// The <see cref="EmailSendingWorker"/> reads from the channel.
/// </summary>
public interface IEmailQueueWriter
{
    /// <summary>Enqueue an email job. Returns false if the channel is full.</summary>
    ValueTask<bool> EnqueueAsync(EmailQueueItem item, CancellationToken ct = default);
}

public interface IEmailQueueReader
{
    /// <summary>Async enumerable that yields items as they become available.</summary>
    IAsyncEnumerable<EmailQueueItem> ReadAllAsync(CancellationToken ct);
}

public sealed class EmailChannel : IEmailQueueWriter, IEmailQueueReader
{
    private readonly Channel<EmailQueueItem> _channel;
    private readonly ILogger<EmailChannel> _logger;

    public EmailChannel(IOptions<EmailQueueOptions> options, ILogger<EmailChannel> logger)
    {
        _logger = logger;
        var opts = options.Value;

        _channel = Channel.CreateBounded<EmailQueueItem>(new BoundedChannelOptions(opts.ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait, // back-pressure; producer awaits
            SingleReader = false,
            SingleWriter = false,
        });
    }

    public async ValueTask<bool> EnqueueAsync(EmailQueueItem item, CancellationToken ct = default)
    {
        try
        {
            await _channel.Writer.WriteAsync(item, ct);
            _logger.LogDebug("Email {EmailId} enqueued (attempt {Attempt}).", item.EmailMessageId, item.Attempt);
            return true;
        }
        catch (ChannelClosedException)
        {
            _logger.LogWarning("Email channel is closed. Cannot enqueue {EmailId}.", item.EmailMessageId);
            return false;
        }
    }

    public IAsyncEnumerable<EmailQueueItem> ReadAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }
}
