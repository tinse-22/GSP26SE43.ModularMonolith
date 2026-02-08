using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.ConfigurationOptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.HostedServices;

public class ReconcilePayOsCheckoutWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly PayOsOptions _payOsOptions;
    private readonly ILogger<ReconcilePayOsCheckoutWorker> _logger;

    public ReconcilePayOsCheckoutWorker(
        IServiceProvider services,
        IOptions<PayOsOptions> payOsOptions,
        ILogger<ReconcilePayOsCheckoutWorker> logger)
    {
        _services = services;
        _payOsOptions = payOsOptions?.Value ?? new PayOsOptions();
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("PayOS checkout reconcile worker is starting.");

        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetDispatcher();
                var command = new ReconcilePayOsCheckoutsCommand();
                await dispatcher.DispatchAsync(command, cancellationToken);

                if (command.ExaminedCount > 0)
                {
                    _logger.LogInformation(
                        "PayOS reconcile examined {Examined}, updated {Updated}, synced {Synced}.",
                        command.ExaminedCount,
                        command.UpdatedCount,
                        command.SyncedCount);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayOS checkout reconcile worker encountered an error.");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(Math.Max(5, _payOsOptions.CheckoutReconcileIntervalSeconds)),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogDebug("PayOS checkout reconcile worker is stopping.");
    }
}
