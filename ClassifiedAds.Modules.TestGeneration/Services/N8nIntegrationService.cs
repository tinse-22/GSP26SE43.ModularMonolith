using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net;
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
        var body = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
        var contentType = response.Content?.Headers.ContentType?.MediaType ?? "(missing)";
        var contentLength = response.Content?.Headers.ContentLength;

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "n8n webhook {WebhookName} failed. Status={Status}, Body={Body}",
                webhookName, response.StatusCode, body);
            throw new ValidationException(
                $"n8n webhook '{webhookName}' tra ve loi. Status: {response.StatusCode}");
        }

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            _logger.LogError(
                "n8n webhook {WebhookName} returned no content. Status={Status}, ContentType={ContentType}, ContentLength={ContentLength}",
                webhookName, response.StatusCode, contentType, contentLength);
            throw new ValidationException(
                $"n8n webhook '{webhookName}' tra ve HTTP 204 va khong co JSON response.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            _logger.LogError(
                "n8n webhook {WebhookName} returned an empty response body. Status={Status}, ContentType={ContentType}, ContentLength={ContentLength}",
                webhookName, response.StatusCode, contentType, contentLength);
            throw new ValidationException(
                $"n8n webhook '{webhookName}' tra ve body rong. He thong dang cho JSON response.");
        }

        if (!IsJsonContentType(contentType))
        {
            _logger.LogWarning(
                "n8n webhook {WebhookName} returned unexpected content type {ContentType}. Attempting JSON deserialization.",
                webhookName, contentType);
        }

        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
            if (result is null)
            {
                _logger.LogError(
                    "n8n webhook {WebhookName} returned a null JSON payload. Status={Status}, ContentType={ContentType}, ContentLength={ContentLength}",
                    webhookName, response.StatusCode, contentType, contentLength);
                throw new ValidationException(
                    $"n8n webhook '{webhookName}' tra ve JSON null. He thong dang cho object hop le.");
            }

            _logger.LogInformation(
                "n8n webhook {WebhookName} succeeded. Status={Status}, ContentType={ContentType}, ContentLength={ContentLength}",
                webhookName, response.StatusCode, contentType, contentLength);

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "n8n webhook {WebhookName} returned invalid JSON. Status={Status}, ContentType={ContentType}, ContentLength={ContentLength}, BodySnippet={BodySnippet}",
                webhookName, response.StatusCode, contentType, contentLength, BuildBodySnippet(body));
            throw new ValidationException(
                $"n8n webhook '{webhookName}' tra ve JSON khong hop le hoac khong dung contract mong doi.",
                ex);
        }
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
            var body = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "n8n webhook {WebhookName} failed. Status={Status}, Body={Body}",
                webhookName, response.StatusCode, body);
            throw new ValidationException(
                $"n8n webhook '{webhookName}' tra ve loi. Status: {response.StatusCode}");
        }

        _logger.LogInformation("n8n webhook {WebhookName} succeeded. Status={Status}", webhookName, response.StatusCode);
    }

    private string ResolveWebhookUrl(string webhookName)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            throw new ValidationException("N8nIntegration chua duoc cau hinh. Vui long thiet lap BaseUrl.");
        }

        if (!_options.Webhooks.TryGetValue(webhookName, out var relativePath) || string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ValidationException(
                $"Khong tim thay cau hinh webhook '{webhookName}' trong N8nIntegration:Webhooks.");
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

    private static bool IsJsonContentType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        return contentType.Contains("json", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildBodySnippet(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return string.Empty;
        }

        const int maxLength = 300;
        var normalized = body.Replace("\r", " ").Replace("\n", " ").Trim();

        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content == null)
        {
            return string.Empty;
        }

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }
}
