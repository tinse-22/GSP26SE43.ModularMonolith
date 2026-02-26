using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public interface IN8nIntegrationService
{
    /// <summary>
    /// Triggers a named n8n webhook with the given payload.
    /// </summary>
    /// <typeparam name="TPayload">Request payload type (serialized as JSON).</typeparam>
    /// <typeparam name="TResponse">Expected response type from n8n workflow.</typeparam>
    /// <param name="webhookName">Logical webhook name configured in N8nIntegration:Webhooks (e.g. "DotnetIntegration").</param>
    /// <param name="payload">The object to POST as JSON body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deserialized response from the n8n webhook.</returns>
    Task<TResponse> TriggerWebhookAsync<TPayload, TResponse>(
        string webhookName,
        TPayload payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Triggers a named n8n webhook with the given payload (fire-and-forget, no response body expected).
    /// </summary>
    Task TriggerWebhookAsync<TPayload>(
        string webhookName,
        TPayload payload,
        CancellationToken cancellationToken = default);
}
