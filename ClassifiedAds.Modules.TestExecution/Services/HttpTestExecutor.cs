using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class HttpTestExecutor : IHttpTestExecutor
{
    private const int MaxResponseBodyLength = 65536;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpTestExecutor> _logger;

    public HttpTestExecutor(
        IHttpClientFactory httpClientFactory,
        ILogger<HttpTestExecutor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<HttpTestResponse> ExecuteAsync(ResolvedTestCaseRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var client = _httpClientFactory.CreateClient("TestExecution");
            client.Timeout = TimeSpan.FromMilliseconds(request.TimeoutMs);

            var httpMethod = new System.Net.Http.HttpMethod(request.HttpMethod);
            var url = BuildUrlWithQueryParams(request.ResolvedUrl, request.QueryParams);

            using var httpRequest = new HttpRequestMessage(httpMethod, url);

            // Set headers
            foreach (var header in request.Headers)
            {
                // Content headers handled separately
                if (IsContentHeader(header.Key))
                {
                    continue;
                }

                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Set body
            if (!string.IsNullOrEmpty(request.Body) && HasBody(request.HttpMethod))
            {
                var contentType = ResolveContentType(request.BodyType, request.Headers);
                httpRequest.Content = new StringContent(request.Body, Encoding.UTF8, contentType);

                // Apply content headers
                foreach (var header in request.Headers.Where(h => IsContentHeader(h.Key)))
                {
                    httpRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            using var response = await client.SendAsync(httpRequest, ct);

            sw.Stop();

            var body = await response.Content.ReadAsStringAsync(ct);
            if (body != null && body.Length > MaxResponseBodyLength)
            {
                body = body[..MaxResponseBodyLength];
            }

            var responseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in response.Headers)
            {
                responseHeaders[h.Key] = string.Join(", ", h.Value);
            }

            foreach (var h in response.Content.Headers)
            {
                responseHeaders[h.Key] = string.Join(", ", h.Value);
            }

            return new HttpTestResponse
            {
                StatusCode = (int)response.StatusCode,
                Headers = responseHeaders,
                Body = body,
                LatencyMs = sw.ElapsedMilliseconds,
                ContentType = response.Content.Headers.ContentType?.MediaType,
            };
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            sw.Stop();
            return new HttpTestResponse
            {
                LatencyMs = sw.ElapsedMilliseconds,
                TransportError = $"Request timeout sau {request.TimeoutMs}ms.",
            };
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            _logger.LogWarning(ex, "HTTP request failed. TestCaseId={TestCaseId}, Url={Url}", request.TestCaseId, request.ResolvedUrl);

            // Provide a clear message for premature connection drops (e.g. Render cold-start)
            var detail = ex.InnerException is System.Net.Http.HttpIOException ioEx
                ? $"Kết nối bị ngắt: {ioEx.HttpRequestError} — server có thể đang khởi động (cold start)."
                : $"Lỗi HTTP: {ex.Message}";

            return new HttpTestResponse
            {
                LatencyMs = sw.ElapsedMilliseconds,
                TransportError = detail,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            _logger.LogWarning(ex, "Unexpected error during HTTP execution. TestCaseId={TestCaseId}", request.TestCaseId);
            return new HttpTestResponse
            {
                LatencyMs = sw.ElapsedMilliseconds,
                TransportError = $"Lỗi không mong muốn: {ex.Message}",
            };
        }
    }

    private static string BuildUrlWithQueryParams(string url, Dictionary<string, string> queryParams)
    {
        if (queryParams == null || queryParams.Count == 0)
        {
            return url;
        }

        var queryItems = queryParams
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value != null)
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}")
            .ToList();

        if (queryItems.Count == 0)
        {
            return url;
        }

        var separator = url.Contains('?') ? "&" : "?";
        var queryString = string.Join("&", queryItems);

        return $"{url}{separator}{queryString}";
    }

    private static string ResolveContentType(string bodyType, Dictionary<string, string> headers)
    {
        // Check if Content-Type already specified in headers
        if (headers.TryGetValue("Content-Type", out var ct) && !string.IsNullOrEmpty(ct))
        {
            return ct;
        }

        return bodyType?.ToUpperInvariant() switch
        {
            "JSON" => "application/json",
            "FORMDATA" => "multipart/form-data",
            "URLENCODED" => "application/x-www-form-urlencoded",
            "RAW" => "text/plain",
            _ => "application/json",
        };
    }

    private static bool HasBody(string httpMethod)
    {
        return httpMethod?.ToUpperInvariant() switch
        {
            "POST" or "PUT" or "PATCH" => true,
            _ => false,
        };
    }

    private static bool IsContentHeader(string headerName)
    {
        return headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
            || headerName.Equals("Content-Length", StringComparison.OrdinalIgnoreCase)
            || headerName.Equals("Content-Encoding", StringComparison.OrdinalIgnoreCase);
    }
}
