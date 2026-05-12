using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Timeout;
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

/// <summary>
/// Result of a webhook trigger operation.
/// </summary>
public class WebhookTriggerResult
{
    public bool Success { get; set; }
    public string WebhookName { get; set; }
    public string ResolvedUrl { get; set; }
    public string ErrorMessage { get; set; }
    public string ErrorDetails { get; set; }
    public bool IsTimeout { get; set; }
    public bool IsNetworkError { get; set; }
}

/// <summary>
/// Represents a transient n8n call failure that can be retried or handled with fallback logic.
/// </summary>
public sealed class N8nTransientException : ValidationException
{
    public N8nTransientException(
        string message,
        string webhookName,
        string resolvedUrl,
        int? statusCode = null,
        bool isTimeout = false,
        bool isNetworkError = false,
        Exception innerException = null)
        : base(message, innerException)
    {
        WebhookName = webhookName;
        ResolvedUrl = resolvedUrl;
        StatusCode = statusCode;
        IsTimeout = isTimeout;
        IsNetworkError = isNetworkError;
    }

    public string WebhookName { get; }

    public string ResolvedUrl { get; }

    public int? StatusCode { get; }

    public bool IsTimeout { get; }

    public bool IsNetworkError { get; }
}

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
        var requestId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Triggering n8n webhook {WebhookName} at {Url}. RequestId={RequestId}, ConfiguredTimeoutSeconds={ConfiguredTimeoutSeconds}, HttpClientTimeoutSeconds={HttpClientTimeoutSeconds}",
            webhookName,
            url,
            requestId,
            _options.TimeoutSeconds,
            (int)_httpClient.Timeout.TotalSeconds);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        ApplyHeaders(request, requestId);

        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            var body = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
            var contentType = response.Content?.Headers.ContentType?.MediaType ?? "(missing)";
            var contentLength = response.Content?.Headers.ContentLength;
            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                if (IsTransientStatusCode(response.StatusCode))
                {
                    var statusCode = (int)response.StatusCode;
                    var isTimeoutLike = IsTimeoutLikeStatusCode(response.StatusCode);

                    _logger.LogWarning(
                        "n8n webhook {WebhookName} returned transient status {StatusCode}. Url={Url}, DurationMs={DurationMs}, RequestId={RequestId}, Body={Body}",
                        webhookName,
                        statusCode,
                        url,
                        stopwatch.ElapsedMilliseconds,
                        requestId,
                        BuildBodySnippet(body));

                    throw new N8nTransientException(
                        isTimeoutLike
                            ? $"n8n webhook '{webhookName}' tam thoi khong phan hoi (HTTP {statusCode}). Vui long kiem tra n8n workflow hoac chia nho payload."
                            : $"n8n webhook '{webhookName}' tam thoi loi (HTTP {statusCode}). Vui long thu lai sau.",
                        webhookName,
                        url,
                        statusCode,
                        isTimeoutLike,
                        false);
                }

                _logger.LogError(
                    "n8n webhook {WebhookName} failed. Status={Status}, Url={Url}, DurationMs={DurationMs}, RequestId={RequestId}, Body={Body}",
                    webhookName,
                    response.StatusCode,
                    url,
                    stopwatch.ElapsedMilliseconds,
                    requestId,
                    body);

                throw new ValidationException(
                    $"n8n webhook '{webhookName}' tra ve loi. Status: {response.StatusCode}");
            }

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                var statusCode = (int)response.StatusCode;

                _logger.LogWarning(
                    "n8n webhook {WebhookName} returned no content (204). Status={Status}, ContentType={ContentType}, ContentLength={ContentLength}, DurationMs={DurationMs}, RequestId={RequestId}",
                    webhookName,
                    response.StatusCode,
                    contentType,
                    contentLength,
                    stopwatch.ElapsedMilliseconds,
                    requestId);

                throw new N8nTransientException(
                    $"n8n webhook '{webhookName}' tra ve HTTP 204 va khong co JSON response.",
                    webhookName,
                    url,
                    statusCode,
                    isTimeout: false,
                    isNetworkError: false);
            }

            if (string.IsNullOrWhiteSpace(body))
            {
                var statusCode = (int)response.StatusCode;

                _logger.LogWarning(
                    "n8n webhook {WebhookName} returned an empty response body. Status={Status}, ContentType={ContentType}, ContentLength={ContentLength}, DurationMs={DurationMs}, RequestId={RequestId}",
                    webhookName,
                    response.StatusCode,
                    contentType,
                    contentLength,
                    stopwatch.ElapsedMilliseconds,
                    requestId);

                throw new N8nTransientException(
                    $"n8n webhook '{webhookName}' tra ve body rong (HTTP {statusCode}). He thong se thu fallback.",
                    webhookName,
                    url,
                    statusCode,
                    isTimeout: false,
                    isNetworkError: false);
            }

            if (!IsJsonContentType(contentType))
            {
                _logger.LogWarning(
                    "n8n webhook {WebhookName} returned unexpected content type {ContentType}. Attempting JSON deserialization. DurationMs={DurationMs}, RequestId={RequestId}",
                    webhookName,
                    contentType,
                    stopwatch.ElapsedMilliseconds,
                    requestId);
            }

            try
            {
                var result = JsonSerializer.Deserialize<TResponse>(body, JsonOptions);
                if (result is null)
                {
                    _logger.LogError(
                        "n8n webhook {WebhookName} returned a null JSON payload. Status={Status}, ContentType={ContentType}, ContentLength={ContentLength}, DurationMs={DurationMs}, RequestId={RequestId}",
                        webhookName,
                        response.StatusCode,
                        contentType,
                        contentLength,
                        stopwatch.ElapsedMilliseconds,
                        requestId);

                    throw new ValidationException(
                        $"n8n webhook '{webhookName}' tra ve JSON null. He thong dang cho object hop le.");
                }

                _logger.LogInformation(
                    "n8n webhook {WebhookName} succeeded. Status={Status}, ContentType={ContentType}, ContentLength={ContentLength}, DurationMs={DurationMs}, RequestId={RequestId}",
                    webhookName,
                    response.StatusCode,
                    contentType,
                    contentLength,
                    stopwatch.ElapsedMilliseconds,
                    requestId);

                return result;
            }
            catch (JsonException ex)
            {
                _logger.LogError(
                    ex,
                    "n8n webhook {WebhookName} returned invalid JSON. Status={Status}, ContentType={ContentType}, ContentLength={ContentLength}, DurationMs={DurationMs}, RequestId={RequestId}, BodySnippet={BodySnippet}",
                    webhookName,
                    response.StatusCode,
                    contentType,
                    contentLength,
                    stopwatch.ElapsedMilliseconds,
                    requestId,
                    BuildBodySnippet(body));

                throw new ValidationException(
                    $"n8n webhook '{webhookName}' tra ve JSON khong hop le hoac khong dung contract mong doi.",
                    ex);
            }
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                ex,
                "n8n webhook {WebhookName} cancelled by caller. Url={Url}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                url,
                stopwatch.ElapsedMilliseconds,
                requestId);

            throw;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "n8n webhook {WebhookName} timed out. Url={Url}, TimeoutSeconds={TimeoutSeconds}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                url,
                _options.TimeoutSeconds,
                stopwatch.ElapsedMilliseconds,
                requestId);

            throw new N8nTransientException(
                $"n8n webhook '{webhookName}' timeout sau {_options.TimeoutSeconds}s. Vui long kiem tra n8n workflow hoac thu lai.",
                webhookName,
                url,
                null,
                true,
                false,
                ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "n8n webhook {WebhookName} was cancelled (likely timeout). Url={Url}, TimeoutSeconds={TimeoutSeconds}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                url,
                _options.TimeoutSeconds,
                stopwatch.ElapsedMilliseconds,
                requestId);

            throw new N8nTransientException(
                $"n8n webhook '{webhookName}' bi huy (timeout). TimeoutSeconds={_options.TimeoutSeconds}.",
                webhookName,
                url,
                null,
                true,
                false,
                ex);
        }
        catch (TimeoutRejectedException ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "n8n webhook {WebhookName} rejected by Polly timeout policy. Url={Url}, TimeoutSeconds={TimeoutSeconds}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                url,
                _options.TimeoutSeconds,
                stopwatch.ElapsedMilliseconds,
                requestId);

            throw new N8nTransientException(
                $"n8n webhook '{webhookName}' bi Polly timeout sau {_options.TimeoutSeconds}s.",
                webhookName,
                url,
                null,
                true,
                false,
                ex);
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "n8n webhook {WebhookName} network error. Url={Url}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                url,
                stopwatch.ElapsedMilliseconds,
                requestId);

            throw new N8nTransientException(
                $"n8n webhook '{webhookName}' loi ket noi: {ex.Message}",
                webhookName,
                url,
                ex.StatusCode.HasValue ? (int)ex.StatusCode.Value : null,
                false,
                true,
                ex);
        }
    }

    public async Task TriggerWebhookAsync<TPayload>(
        string webhookName,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        var result = await TriggerWebhookWithResultAsync(webhookName, payload, cancellationToken);

        if (!result.Success)
        {
            throw new ValidationException(result.ErrorMessage);
        }
    }

    /// <summary>
    /// Triggers an n8n webhook and returns a result object instead of throwing.
    /// Use this for background processing where you want to handle errors gracefully.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<WebhookTriggerResult> TriggerWebhookWithResultAsync<TPayload>(
        string webhookName,
        TPayload payload,
        CancellationToken cancellationToken = default)
    {
        string url;
        var requestId = Guid.NewGuid().ToString("N");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            url = ResolveWebhookUrl(webhookName);
        }
        catch (ValidationException ex)
        {
            return new WebhookTriggerResult
            {
                Success = false,
                WebhookName = webhookName,
                ErrorMessage = ex.Message,
                ErrorDetails = "Webhook configuration error"
            };
        }

        _logger.LogInformation(
            "Triggering n8n webhook {WebhookName} at {Url} (result-based). RequestId={RequestId}, TimeoutSeconds={TimeoutSeconds}, BeBaseUrl={BeBaseUrl}, HttpClientTimeoutSeconds={HttpClientTimeoutSeconds}",
            webhookName,
            url,
            requestId,
            _options.TimeoutSeconds,
            _options.BeBaseUrl,
            (int)_httpClient.Timeout.TotalSeconds);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        ApplyHeaders(request, requestId);

        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            stopwatch.Stop();

            if (!response.IsSuccessStatusCode)
            {
                var body = await ReadResponseBodyAsync(response, cancellationToken).ConfigureAwait(false);
                _logger.LogError(
                    "n8n webhook {WebhookName} failed. Status={Status}, Url={Url}, DurationMs={DurationMs}, RequestId={RequestId}, Body={Body}",
                    webhookName,
                    response.StatusCode,
                    url,
                    stopwatch.ElapsedMilliseconds,
                    requestId,
                    body);

                return new WebhookTriggerResult
                {
                    Success = false,
                    WebhookName = webhookName,
                    ResolvedUrl = url,
                    ErrorMessage = $"n8n webhook '{webhookName}' tra ve loi. Status: {response.StatusCode}",
                    ErrorDetails = body
                };
            }

            _logger.LogInformation(
                "n8n webhook {WebhookName} succeeded. Status={Status}, Url={Url}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                response.StatusCode,
                url,
                stopwatch.ElapsedMilliseconds,
                requestId);

            return new WebhookTriggerResult
            {
                Success = true,
                WebhookName = webhookName,
                ResolvedUrl = url
            };
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogWarning(
                ex,
                "n8n webhook {WebhookName} cancelled by caller. Url={Url}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                url,
                stopwatch.ElapsedMilliseconds,
                requestId);

            throw;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "n8n webhook {WebhookName} timed out. Url={Url}, TimeoutSeconds={TimeoutSeconds}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                url,
                _options.TimeoutSeconds,
                stopwatch.ElapsedMilliseconds,
                requestId);

            return new WebhookTriggerResult
            {
                Success = false,
                WebhookName = webhookName,
                ResolvedUrl = url,
                IsTimeout = true,
                ErrorMessage = $"n8n webhook '{webhookName}' timeout sau {_options.TimeoutSeconds}s. Vui long kiem tra n8n workflow hoac tang timeout.",
                ErrorDetails = ex.ToString()
            };
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "n8n webhook {WebhookName} was cancelled (likely timeout). Url={Url}, TimeoutSeconds={TimeoutSeconds}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                url,
                _options.TimeoutSeconds,
                stopwatch.ElapsedMilliseconds,
                requestId);

            return new WebhookTriggerResult
            {
                Success = false,
                WebhookName = webhookName,
                ResolvedUrl = url,
                IsTimeout = true,
                ErrorMessage = $"n8n webhook '{webhookName}' bi huy (timeout). TimeoutSeconds={_options.TimeoutSeconds}",
                ErrorDetails = ex.ToString()
            };
        }
        catch (TimeoutRejectedException ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "n8n webhook {WebhookName} rejected by Polly timeout policy. Url={Url}, TimeoutSeconds={TimeoutSeconds}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                url,
                _options.TimeoutSeconds,
                stopwatch.ElapsedMilliseconds,
                requestId);

            return new WebhookTriggerResult
            {
                Success = false,
                WebhookName = webhookName,
                ResolvedUrl = url,
                IsTimeout = true,
                ErrorMessage = $"n8n webhook '{webhookName}' bi Polly timeout sau {_options.TimeoutSeconds}s.",
                ErrorDetails = ex.ToString()
            };
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "n8n webhook {WebhookName} network error. Url={Url}, DurationMs={DurationMs}, RequestId={RequestId}",
                webhookName,
                url,
                stopwatch.ElapsedMilliseconds,
                requestId);

            return new WebhookTriggerResult
            {
                Success = false,
                WebhookName = webhookName,
                ResolvedUrl = url,
                IsNetworkError = true,
                ErrorMessage = $"n8n webhook '{webhookName}' loi ket noi: {ex.Message}",
                ErrorDetails = ex.ToString()
            };
        }
    }

    /// <summary>
    /// Resolves the full webhook URL for a given webhook name.
    /// </summary>
    /// <returns></returns>
    public string GetResolvedWebhookUrl(string webhookName)
    {
        return ResolveWebhookUrl(webhookName);
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

    private void ApplyHeaders(HttpRequestMessage request, string requestId)
    {
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);
        }

        if (!string.IsNullOrWhiteSpace(requestId))
        {
            request.Headers.TryAddWithoutValidation("x-request-id", requestId);
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

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is 408 or 429 or 500 or 502 or 503 or 504 or 520 or 522 or 524;
    }

    private static bool IsTimeoutLikeStatusCode(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is 408 or 504 or 522 or 524;
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
