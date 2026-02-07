using ClassifiedAds.Modules.Notification.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Notification.HostedServices;

public class SendSmsWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SendSmsWorker> _logger;

    public SendSmsWorker(IServiceProvider services,
        ILogger<SendSmsWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("SendSmsService is starting.");
        try
        {
            await DoWork(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("SendSmsWorker cancelled.");
        }
    }

    private async Task DoWork(CancellationToken cancellationToken)
    {
        // Wait briefly for infrastructure (DB, etc.) to become available
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug($"SendSms task doing background work.");

                var sendSmsCommand = new SendSmsMessagesCommand();

                using (var scope = _services.CreateScope())
                {
                    var dispatcher = scope.ServiceProvider.GetDispatcher();

                    await dispatcher.DispatchAsync(sendSmsCommand, cancellationToken);
                }

                if (sendSmsCommand.SentMessagesCount == 0)
                {
                    await Task.Delay(10000, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                _logger.LogDebug("SendSmsWorker stopping because service provider has been disposed.");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SendSmsWorker encountered an error. Retrying in 15 seconds...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }

        _logger.LogDebug($"ResendSms background task is stopping.");
    }
}
