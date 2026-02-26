using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public class N8nIntegrationService : IN8nIntegrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly N8nIntegrationOptions _options;
    private readonly ILogger<N8nIntegrationService> _logger;

    public N8nIntegrationService(
        HttpClient httpClient,
        IOptions<N8nIntegrationOptions> options,
        ILogger<N8nIntegrationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> TriggerWebhookAsync<TPayload, TResponse>(
        string webhookName,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        var url = ResolveWebhookUrl(webhookName);

        _logger.LogInformation("Triggering n8n webhook {WebhookName} at {Url}", webhookName, url);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        ApplyHeaders(request);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "n8n webhook {WebhookName} failed. Status={Status}, Body={Body}",
                webhookName, response.StatusCode, body);
            throw new ValidationException(
                $"n8n webhook '{webhookName}' trả về lỗi. Status: {response.StatusCode}");
        }

        _logger.LogInformation("n8n webhook {WebhookName} succeeded. Status={Status}", webhookName, response.StatusCode);

        return JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
    }

    public async Task TriggerWebhookAsync<TPayload>(
        string webhookName,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        var url = ResolveWebhookUrl(webhookName);

        _logger.LogInformation("Triggering n8n webhook {WebhookName} at {Url} (fire-and-forget)", webhookName, url);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        ApplyHeaders(request);

        var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "n8n webhook {WebhookName} failed. Status={Status}, Body={Body}",
                webhookName, response.StatusCode, body);
            throw new ValidationException(
                $"n8n webhook '{webhookName}' trả về lỗi. Status: {response.StatusCode}");
        }

        _logger.LogInformation("n8n webhook {WebhookName} succeeded. Status={Status}", webhookName, response.StatusCode);
    }

    private string ResolveWebhookUrl(string webhookName)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new ValidationException("N8nIntegration chưa được cấu hình. Vui lòng thiết lập BaseUrl.");
        }

        if (!_options.Webhooks.TryGetValue(webhookName, out var relativePath) || string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ValidationException(
                $"Không tìm thấy cấu hình webhook '{webhookName}' trong N8nIntegration:Webhooks.");
        }

        return $"{_options.BaseUrl.TrimEnd('/')}/{relativePath.TrimStart('/')}";
    }

    private void ApplyHeaders(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        }
    }
}
