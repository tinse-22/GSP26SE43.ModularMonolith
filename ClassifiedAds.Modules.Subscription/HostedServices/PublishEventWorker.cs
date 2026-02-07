using ClassifiedAds.Application.FeatureToggles;
using ClassifiedAds.Modules.Subscription.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.HostedServices;

public class PublishEventWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IOutboxPublishingToggle _outboxPublishingToggle;
    private readonly ILogger<PublishEventWorker> _logger;

    public PublishEventWorker(IServiceProvider services,
        IOutboxPublishingToggle outboxPublishingToggle,
        ILogger<PublishEventWorker> logger)
    {
        _services = services;
        _outboxPublishingToggle = outboxPublishingToggle;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Worker phát sự kiện outbox của Subscription đang khởi động.");
        await DoWork(cancellationToken);
    }

    private async Task DoWork(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_outboxPublishingToggle.IsEnabled())
            {
                _logger.LogInformation("Worker outbox Subscription đang tạm dừng. Sẽ thử lại sau 10 giây.");
                try
                {
                    await Task.Delay(10000, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                continue;
            }

            _logger.LogDebug("Worker outbox Subscription đang xử lý nền.");

            try
            {
                var publishEventsCommand = new PublishEventsCommand();

                using (var scope = _services.CreateScope())
                {
                    var dispatcher = scope.ServiceProvider.GetDispatcher();

                    await dispatcher.DispatchAsync(publishEventsCommand, cancellationToken);
                }

                if (publishEventsCommand.SentEventsCount == 0)
                {
                    await Task.Delay(10000, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Có lỗi khi phát sự kiện outbox của Subscription.");
                try
                {
                    await Task.Delay(10000, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogDebug("Worker phát sự kiện outbox của Subscription đang dừng.");
    }
}
