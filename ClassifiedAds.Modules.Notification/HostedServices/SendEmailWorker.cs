using ClassifiedAds.Modules.Notification.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Notification.HostedServices;

public class SendEmailWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<SendEmailWorker> _logger;

    public SendEmailWorker(IServiceProvider services,
        ILogger<SendEmailWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("SendEmailService is starting.");
        await DoWork(cancellationToken);
    }

    private async Task DoWork(CancellationToken cancellationToken)
    {
        // Wait briefly for infrastructure (DB, etc.) to become available
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug($"SendEmail task doing background work.");

                var sendEmailsCommand = new SendEmailMessagesCommand();

                using (var scope = _services.CreateScope())
                {
                    var dispatcher = scope.ServiceProvider.GetDispatcher();

                    await dispatcher.DispatchAsync(sendEmailsCommand, cancellationToken);
                }

                if (sendEmailsCommand.SentMessagesCount == 0)
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
                _logger.LogError(ex, "SendEmailWorker encountered an error. Retrying in 15 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken);
            }
        }

        _logger.LogDebug($"SendEmail background task is stopping.");
    }
}