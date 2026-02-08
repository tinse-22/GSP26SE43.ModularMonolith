using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.Subscription.ConfigurationOptions;
using ClassifiedAds.Modules.Subscription.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.Subscription.Services;

public class PayOsService : IPayOsService
{
    private readonly HttpClient _httpClient;
    private readonly PayOsOptions _options;
    private readonly ILogger<PayOsService> _logger;

    public PayOsService(
        HttpClient httpClient,
        IOptions<PayOsOptions> options,
        ILogger<PayOsService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> CreatePaymentLinkAsync(PayOsCreatePaymentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        EnsurePayOsConfigured();

        var returnUrl = string.IsNullOrWhiteSpace(request.ReturnUrl) ? _options.ReturnUrl : request.ReturnUrl;
        var cancelUrl = string.IsNullOrWhiteSpace(request.CancelUrl)
            ? (string.IsNullOrWhiteSpace(_options.CancelUrl) ? returnUrl : _options.CancelUrl)
            : request.CancelUrl;
        var description = request.Description ?? string.Empty;

        var signatureData = $"amount={request.Amount}&cancelUrl={cancelUrl}&description={description}&orderCode={request.OrderCode}&returnUrl={returnUrl}";
        var signature = ComputeHmacSha256(signatureData, _options.SecretKey);

        var payload = new
        {
            orderCode = request.OrderCode,
            amount = request.Amount,
            description,
            returnUrl,
            cancelUrl,
            buyerName = request.BuyerName,
            buyerEmail = request.BuyerEmail,
            buyerPhone = request.BuyerPhone,
            signature,
        };

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/v2/payment-requests";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(payload),
        };
        httpRequest.Headers.TryAddWithoutValidation("x-client-id", _options.ClientId);
        httpRequest.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);

        var response = await _httpClient.SendAsync(httpRequest, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("PayOS CreatePaymentLink failed. Status={Status}, Body={Body}", response.StatusCode, body);
            throw new ValidationException($"Failed to create PayOS payment link. Status: {response.StatusCode}");
        }

        var parsed = JsonSerializer.Deserialize<PayOsPaymentResponse>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (parsed is null || !string.Equals(parsed.Code, "00", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException($"PayOS error: {parsed?.Desc ?? "Unknown"}");
        }

        var checkoutUrl = parsed.Data?.CheckoutUrl;
        if (string.IsNullOrWhiteSpace(checkoutUrl))
        {
            throw new ValidationException("PayOS response missing checkout URL.");
        }

        return checkoutUrl;
    }

    public bool VerifyWebhookSignature(PayOsWebhookPayload payload, string rawBody)
    {
        if (payload?.Data is null || string.IsNullOrWhiteSpace(payload.Signature) || string.IsNullOrWhiteSpace(rawBody))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return false;
        }

        using var doc = JsonDocument.Parse(rawBody);
        if (!doc.RootElement.TryGetProperty("data", out var dataElement))
        {
            return false;
        }

        var dictionary = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataElement.GetRawText());
        if (dictionary == null)
        {
            return false;
        }

        var sortedPairs = dictionary
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => $"{x.Key}={NormalizeValue(x.Value)}");
        var sortedData = string.Join("&", sortedPairs);

        var expectedSignature = ComputeHmacSha256(sortedData, _options.SecretKey);
        return string.Equals(expectedSignature, payload.Signature, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<PayOsGetPaymentData> GetPaymentInfoAsync(long orderCode, CancellationToken ct = default)
    {
        EnsurePayOsConfigured();

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/v2/payment-requests/{orderCode}";
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.TryAddWithoutValidation("x-client-id", _options.ClientId);
        request.Headers.TryAddWithoutValidation("x-api-key", _options.ApiKey);

        var response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new ValidationException($"Failed to get PayOS payment info. Status: {response.StatusCode}");
        }

        var parsed = JsonSerializer.Deserialize<PayOsGetPaymentResponse>(body, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        });

        if (parsed?.Data is null)
        {
            throw new ValidationException("PayOS response missing data.");
        }

        return parsed.Data;
    }

    private void EnsurePayOsConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId)
            || string.IsNullOrWhiteSpace(_options.ApiKey)
            || string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new ValidationException("PayOS is not configured. Please set ClientId, ApiKey and SecretKey.");
        }
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private static string NormalizeValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => string.Empty,
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number => element.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => element.GetRawText(),
        };
    }
}