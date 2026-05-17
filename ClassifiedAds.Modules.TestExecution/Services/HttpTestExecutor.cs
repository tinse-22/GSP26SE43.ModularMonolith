using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
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
                httpRequest.Content = CreateHttpContent(request);

                // Apply content headers
                foreach (var header in request.Headers.Where(h => IsContentHeader(h.Key)))
                {
                    if (ShouldSkipManagedContentHeader(request.BodyType, header.Key))
                    {
                        continue;
                    }

                    if (header.Key.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        httpRequest.Content.Headers.Remove(header.Key);
                    }

                    httpRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
            }

            using var response = await client.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                ct);

            sw.Stop();

            var body = await ReadBodyPreviewAsync(response.Content, ct);

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
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
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

    private static async Task<string> ReadBodyPreviewAsync(HttpContent content, CancellationToken ct)
    {
        if (content == null)
        {
            return null;
        }

        await using var stream = await content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: false);

        var buffer = new char[Math.Min(8192, MaxResponseBodyLength)];
        var builder = new StringBuilder(capacity: Math.Min(MaxResponseBodyLength, 8192));

        while (builder.Length < MaxResponseBodyLength)
        {
            var remaining = MaxResponseBodyLength - builder.Length;
            var read = await reader.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), ct);
            if (read == 0)
            {
                break;
            }

            builder.Append(buffer, 0, read);
        }

        return builder.ToString();
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

    private static HttpContent CreateHttpContent(ResolvedTestCaseRequest request)
    {
        var normalizedBodyType = NormalizeBodyType(request.BodyType);

        return normalizedBodyType switch
        {
            "FORMDATA" => CreateMultipartFormDataContent(request.Body),
            "URLENCODED" => CreateFormUrlEncodedContent(request.Body),
            _ => CreateTextContent(request.Body, ResolveContentType(request.BodyType, request.Headers)),
        };
    }

    private static HttpContent CreateTextContent(string body, string contentType)
    {
        var content = new StringContent(body ?? string.Empty, Encoding.UTF8);

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            content.Headers.Remove("Content-Type");
            content.Headers.TryAddWithoutValidation("Content-Type", contentType);
        }

        return content;
    }

    private static HttpContent CreateFormUrlEncodedContent(string body)
    {
        if (TryParseBodyObject(body, out var bodyObject))
        {
            return new FormUrlEncodedContent(BuildFormFields(bodyObject));
        }

        return CreateTextContent(body, "application/x-www-form-urlencoded");
    }

    private static HttpContent CreateMultipartFormDataContent(string body)
    {
        var content = new MultipartFormDataContent();

        if (TryParseBodyObject(body, out var bodyObject))
        {
            foreach (var property in bodyObject)
            {
                AddMultipartPart(content, property.Key, property.Value);
            }

            return content;
        }

        content.Add(new StringContent(body ?? string.Empty, Encoding.UTF8), "payload");
        return content;
    }

    private static IEnumerable<KeyValuePair<string, string>> BuildFormFields(JsonObject bodyObject)
    {
        foreach (var property in bodyObject)
        {
            foreach (var value in EnumerateNodeValues(property.Value))
            {
                yield return new KeyValuePair<string, string>(property.Key, ConvertNodeToString(value));
            }
        }
    }

    private static void AddMultipartPart(MultipartFormDataContent content, string name, JsonNode node)
    {
        foreach (var value in EnumerateNodeValues(node))
        {
            if (IsFileLikeField(name, value))
            {
                var fileName = ResolveFileName(name, value);
                var fileBytes = Encoding.UTF8.GetBytes("sample file content");
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream");
                content.Add(fileContent, name, fileName);
                continue;
            }

            content.Add(new StringContent(ConvertNodeToString(value) ?? string.Empty, Encoding.UTF8), name);
        }
    }

    private static IEnumerable<JsonNode> EnumerateNodeValues(JsonNode node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                yield return item;
            }

            yield break;
        }

        yield return node;
    }

    private static bool TryParseBodyObject(string body, out JsonObject bodyObject)
    {
        try
        {
            bodyObject = JsonNode.Parse(body) as JsonObject;
            return bodyObject != null;
        }
        catch
        {
            bodyObject = null;
            return false;
        }
    }

    private static string ConvertNodeToString(JsonNode node)
    {
        if (node == null)
        {
            return string.Empty;
        }

        if (node is JsonValue value)
        {
            if (value.TryGetValue<string>(out var stringValue))
            {
                return stringValue ?? string.Empty;
            }

            try
            {
                using var document = JsonDocument.Parse(value.ToJsonString());
                return document.RootElement.ToString();
            }
            catch
            {
                return value.ToJsonString().Trim('"');
            }
        }

        return node.ToJsonString();
    }

    private static bool IsFileLikeField(string fieldName, JsonNode node)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        if (fieldName.IndexOf("file", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fieldName.IndexOf("image", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fieldName.IndexOf("attachment", StringComparison.OrdinalIgnoreCase) >= 0 ||
            fieldName.IndexOf("avatar", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        var value = ConvertNodeToString(node);
        return value.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveFileName(string fieldName, JsonNode node)
    {
        var value = ConvertNodeToString(node);
        if (!string.IsNullOrWhiteSpace(value) && value.Contains('.', StringComparison.Ordinal))
        {
            return value;
        }

        return string.IsNullOrWhiteSpace(fieldName)
            ? "upload.txt"
            : $"{fieldName}.txt";
    }

    private static bool ShouldSkipManagedContentHeader(string bodyType, string headerName)
    {
        return headerName.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)
            && (NormalizeBodyType(bodyType) == "FORMDATA" || NormalizeBodyType(bodyType) == "URLENCODED");
    }

    private static string NormalizeBodyType(string bodyType)
    {
        if (string.IsNullOrWhiteSpace(bodyType))
        {
            return string.Empty;
        }

        return bodyType
            .Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
    }

    private static bool HasBody(string httpMethod)
    {
        return httpMethod?.ToUpperInvariant() switch
        {
            "POST" or "PUT" or "PATCH" or "DELETE" => true,
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
