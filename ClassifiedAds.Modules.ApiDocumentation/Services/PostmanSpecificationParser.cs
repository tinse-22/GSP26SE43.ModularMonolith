using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Services;

public class PostmanSpecificationParser : ISpecificationParser
{
    private static readonly Regex SlugifyRegex = new(@"[^a-zA-Z0-9_]+", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> HttpMethodMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["GET"] = "GET",
        ["POST"] = "POST",
        ["PUT"] = "PUT",
        ["DELETE"] = "DELETE",
        ["PATCH"] = "PATCH",
        ["HEAD"] = "HEAD",
        ["OPTIONS"] = "OPTIONS",
    };

    public bool CanParse(SourceType sourceType)
    {
        return sourceType == SourceType.Postman;
    }

    public Task<SpecificationParseResult> ParseAsync(byte[] content, string fileName, CancellationToken cancellationToken = default)
    {
        var result = new SpecificationParseResult();

        if (content == null || content.Length == 0)
        {
            result.Success = false;
            result.Errors.Add("File content is empty.");
            return Task.FromResult(result);
        }

        try
        {
            var json = Encoding.UTF8.GetString(content);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (!root.TryGetProperty("info", out var infoElement))
            {
                result.Success = false;
                result.Errors.Add("Missing 'info' property. Not a valid Postman Collection.");
                return Task.FromResult(result);
            }

            if (!root.TryGetProperty("item", out var itemElement))
            {
                result.Success = false;
                result.Errors.Add("Missing 'item' property. Not a valid Postman Collection.");
                return Task.FromResult(result);
            }

            // Extract version
            if (infoElement.TryGetProperty("version", out var versionElement))
            {
                result.DetectedVersion = versionElement.ValueKind == JsonValueKind.String
                    ? versionElement.GetString()
                    : null;
            }

            if (string.IsNullOrWhiteSpace(result.DetectedVersion) && infoElement.TryGetProperty("schema", out var schemaElement))
            {
                var schemaUrl = schemaElement.GetString();
                if (!string.IsNullOrWhiteSpace(schemaUrl) && schemaUrl.Contains("v2.1"))
                {
                    result.DetectedVersion = "2.1.0";
                }
                else if (!string.IsNullOrWhiteSpace(schemaUrl) && schemaUrl.Contains("v2.0"))
                {
                    result.DetectedVersion = "2.0.0";
                }
            }

            // Parse collection-level auth
            ParsedSecurityScheme collectionAuth = null;
            ParsedSecurityRequirement collectionAuthReq = null;
            if (root.TryGetProperty("auth", out var collectionAuthElement))
            {
                (collectionAuth, collectionAuthReq) = ParseAuth(collectionAuthElement);
                if (collectionAuth != null)
                {
                    result.SecuritySchemes.Add(collectionAuth);
                }
            }

            // Flatten and parse items
            var flatItems = new List<JsonElement>();
            FlattenItems(itemElement, flatItems);

            foreach (var item in flatItems)
            {
                var endpoint = ParseItem(item, collectionAuthReq);
                if (endpoint != null)
                {
                    result.Endpoints.Add(endpoint);

                    // Collect per-request auth schemes
                    if (item.TryGetProperty("request", out var requestElement) && requestElement.TryGetProperty("auth", out var authElement))
                    {
                        var (scheme, _) = ParseAuth(authElement);
                        if (scheme != null && !result.SecuritySchemes.Any(s => s.Name == scheme.Name))
                        {
                            result.SecuritySchemes.Add(scheme);
                        }
                    }
                }
            }

            result.Success = true;
        }
        catch (JsonException ex)
        {
            result.Success = false;
            result.Errors.Add($"Invalid JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Errors.Add($"Failed to parse Postman Collection: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    private static void FlattenItems(JsonElement itemsElement, List<JsonElement> flatItems)
    {
        if (itemsElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in itemsElement.EnumerateArray())
        {
            // If item has nested "item" array, it's a folder - recurse
            if (item.TryGetProperty("item", out var nestedItems))
            {
                FlattenItems(nestedItems, flatItems);
            }
            else if (item.TryGetProperty("request", out _))
            {
                // It's a request item
                flatItems.Add(item);
            }
        }
    }

    private static ParsedEndpoint ParseItem(JsonElement item, ParsedSecurityRequirement collectionAuthReq)
    {
        if (!item.TryGetProperty("request", out var requestElement))
        {
            return null;
        }

        // Handle request as string (simple form) or object
        string method = "GET";
        string path = "/";
        string summary = null;
        string description = null;

        if (item.TryGetProperty("name", out var nameElement))
        {
            summary = nameElement.GetString();
        }

        if (requestElement.ValueKind == JsonValueKind.String)
        {
            // Simple request: just a URL string
            path = NormalizePath(requestElement.GetString());
            return new ParsedEndpoint
            {
                HttpMethod = method,
                Path = path,
                OperationId = Slugify(summary),
                Summary = summary,
            };
        }

        // Extract HTTP method
        if (requestElement.TryGetProperty("method", out var methodElement))
        {
            var rawMethod = methodElement.GetString()?.ToUpperInvariant();
            method = HttpMethodMap.TryGetValue(rawMethod ?? "GET", out var mapped) ? mapped : "GET";
        }

        // Extract description
        if (requestElement.TryGetProperty("description", out var descElement))
        {
            description = descElement.ValueKind == JsonValueKind.String
                ? descElement.GetString()
                : null;
        }

        // Extract URL/path
        if (requestElement.TryGetProperty("url", out var urlElement))
        {
            path = ExtractPath(urlElement);
        }

        var endpoint = new ParsedEndpoint
        {
            HttpMethod = method,
            Path = path,
            OperationId = Slugify(summary),
            Summary = summary,
            Description = description,
        };

        // Parse URL variables as path parameters
        if (requestElement.TryGetProperty("url", out var urlEl) && urlEl.ValueKind == JsonValueKind.Object)
        {
            if (urlEl.TryGetProperty("variable", out var variablesElement) && variablesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var variable in variablesElement.EnumerateArray())
                {
                    var paramName = GetStringProperty(variable, "key");
                    if (string.IsNullOrWhiteSpace(paramName))
                    {
                        continue;
                    }

                    endpoint.Parameters.Add(new ParsedParameter
                    {
                        Name = paramName,
                        Location = "Path",
                        DataType = "string",
                        IsRequired = true,
                        DefaultValue = GetStringProperty(variable, "value"),
                        Examples = GetStringProperty(variable, "value"),
                    });
                }
            }

            // Parse query parameters
            if (urlEl.TryGetProperty("query", out var queryElement) && queryElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var queryParam in queryElement.EnumerateArray())
                {
                    var paramName = GetStringProperty(queryParam, "key");
                    if (string.IsNullOrWhiteSpace(paramName))
                    {
                        continue;
                    }

                    endpoint.Parameters.Add(new ParsedParameter
                    {
                        Name = paramName,
                        Location = "Query",
                        DataType = "string",
                        IsRequired = false,
                        DefaultValue = GetStringProperty(queryParam, "value"),
                    });
                }
            }
        }

        // Parse headers
        if (requestElement.TryGetProperty("header", out var headerElement) && headerElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var header in headerElement.EnumerateArray())
            {
                var headerName = GetStringProperty(header, "key");
                if (string.IsNullOrWhiteSpace(headerName))
                {
                    continue;
                }

                // Skip common auto-headers
                if (string.Equals(headerName, "Content-Type", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(headerName, "Accept", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                endpoint.Parameters.Add(new ParsedParameter
                {
                    Name = headerName,
                    Location = "Header",
                    DataType = "string",
                    IsRequired = false,
                    DefaultValue = GetStringProperty(header, "value"),
                });
            }
        }

        // Parse request body
        if (requestElement.TryGetProperty("body", out var bodyElement) && bodyElement.ValueKind == JsonValueKind.Object)
        {
            var bodyParam = ParseBody(bodyElement);
            if (bodyParam != null)
            {
                endpoint.Parameters.Add(bodyParam);
            }
        }

        // Parse responses
        if (item.TryGetProperty("response", out var responsesElement) && responsesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var responseItem in responsesElement.EnumerateArray())
            {
                var response = ParseResponse(responseItem);
                if (response != null)
                {
                    endpoint.Responses.Add(response);
                }
            }
        }

        // Parse request-level auth
        if (requestElement.TryGetProperty("auth", out var authElement))
        {
            var (_, secReq) = ParseAuth(authElement);
            if (secReq != null)
            {
                endpoint.SecurityRequirements.Add(secReq);
            }
        }
        else if (collectionAuthReq != null)
        {
            // Inherit collection-level auth
            endpoint.SecurityRequirements.Add(collectionAuthReq);
        }

        return endpoint;
    }

    private static string ExtractPath(JsonElement urlElement)
    {
        if (urlElement.ValueKind == JsonValueKind.String)
        {
            return NormalizePath(urlElement.GetString());
        }

        if (urlElement.ValueKind != JsonValueKind.Object)
        {
            return "/";
        }

        // Try path array first
        if (urlElement.TryGetProperty("path", out var pathElement) && pathElement.ValueKind == JsonValueKind.Array)
        {
            var segments = new List<string>();
            foreach (var segment in pathElement.EnumerateArray())
            {
                var value = segment.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    // Convert :paramName to {paramName}
                    if (value.StartsWith(':'))
                    {
                        segments.Add("{" + value.Substring(1) + "}");
                    }
                    else
                    {
                        segments.Add(value);
                    }
                }
            }

            return "/" + string.Join("/", segments);
        }

        // Fallback to raw URL
        if (urlElement.TryGetProperty("raw", out var rawElement))
        {
            return NormalizePath(rawElement.GetString());
        }

        return "/";
    }

    private static string NormalizePath(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return "/";
        }

        // Remove protocol and host parts
        var url = rawUrl.Trim();

        // If it looks like a full URL, extract the path
        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(url);
                url = uri.AbsolutePath;
            }
            catch
            {
                // If URI parsing fails, try to extract path manually
                var pathStart = url.IndexOf('/', url.IndexOf("://") + 3);
                url = pathStart >= 0 ? url.Substring(pathStart) : "/";
            }
        }

        // Remove query string
        var queryStart = url.IndexOf('?');
        if (queryStart >= 0)
        {
            url = url.Substring(0, queryStart);
        }

        // Replace {{variable}} with {variable} and :param with {param}
        url = Regex.Replace(url, @"\{\{(\w+)\}\}", "{$1}");
        url = Regex.Replace(url, @":(\w+)", "{$1}");

        // Ensure starts with /
        if (!url.StartsWith('/'))
        {
            url = "/" + url;
        }

        return url;
    }

    private static ParsedParameter ParseBody(JsonElement bodyElement)
    {
        var mode = GetStringProperty(bodyElement, "mode");

        string schemaJson = null;

        if (string.Equals(mode, "raw", StringComparison.OrdinalIgnoreCase))
        {
            var raw = GetStringProperty(bodyElement, "raw");
            if (!string.IsNullOrWhiteSpace(raw))
            {
                // Try to parse as JSON to create a schema-like representation
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    schemaJson = JsonSerializer.Serialize(new { type = "object", example = raw });
                }
                catch
                {
                    schemaJson = JsonSerializer.Serialize(new { type = "string", example = raw });
                }
            }
        }
        else if (string.Equals(mode, "urlencoded", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(mode, "formdata", StringComparison.OrdinalIgnoreCase))
        {
            if (bodyElement.TryGetProperty(mode, out var formData) && formData.ValueKind == JsonValueKind.Array)
            {
                var properties = new Dictionary<string, object>();
                foreach (var field in formData.EnumerateArray())
                {
                    var key = GetStringProperty(field, "key");
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        properties[key] = new { type = "string" };
                    }
                }

                if (properties.Count > 0)
                {
                    schemaJson = JsonSerializer.Serialize(new { type = "object", properties });
                }
            }
        }

        if (schemaJson == null)
        {
            return null;
        }

        return new ParsedParameter
        {
            Name = "body",
            Location = "Body",
            DataType = "object",
            IsRequired = true,
            Schema = schemaJson,
        };
    }

    private static ParsedResponse ParseResponse(JsonElement responseElement)
    {
        int statusCode = 200;
        if (responseElement.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.Number)
        {
            statusCode = codeElement.GetInt32();
        }

        var description = GetStringProperty(responseElement, "name")
                          ?? GetStringProperty(responseElement, "status");

        string examplesJson = null;
        var body = GetStringProperty(responseElement, "body");
        if (!string.IsNullOrWhiteSpace(body))
        {
            examplesJson = body;
        }

        string headersJson = null;
        if (responseElement.TryGetProperty("header", out var headersElement) && headersElement.ValueKind == JsonValueKind.Array)
        {
            var headerDict = new Dictionary<string, object>();
            foreach (var header in headersElement.EnumerateArray())
            {
                var key = GetStringProperty(header, "key");
                var value = GetStringProperty(header, "value");
                if (!string.IsNullOrWhiteSpace(key))
                {
                    headerDict[key] = new { value };
                }
            }

            if (headerDict.Count > 0)
            {
                headersJson = JsonSerializer.Serialize(headerDict);
            }
        }

        return new ParsedResponse
        {
            StatusCode = statusCode,
            Description = description,
            Examples = examplesJson,
            Headers = headersJson,
        };
    }

    private static (ParsedSecurityScheme Scheme, ParsedSecurityRequirement Requirement) ParseAuth(JsonElement authElement)
    {
        if (authElement.ValueKind != JsonValueKind.Object)
        {
            return (null, null);
        }

        var authType = GetStringProperty(authElement, "type");
        if (string.IsNullOrWhiteSpace(authType) || string.Equals(authType, "noauth", StringComparison.OrdinalIgnoreCase))
        {
            return (null, null);
        }

        ParsedSecurityScheme scheme;
        ParsedSecurityRequirement requirement;

        switch (authType.ToLowerInvariant())
        {
            case "bearer":
                scheme = new ParsedSecurityScheme
                {
                    Name = "bearerAuth",
                    SchemeType = SchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                };
                requirement = new ParsedSecurityRequirement
                {
                    SecurityType = SecurityType.Bearer,
                    SchemeName = "bearerAuth",
                };
                break;

            case "basic":
                scheme = new ParsedSecurityScheme
                {
                    Name = "basicAuth",
                    SchemeType = SchemeType.Http,
                    Scheme = "basic",
                };
                requirement = new ParsedSecurityRequirement
                {
                    SecurityType = SecurityType.Basic,
                    SchemeName = "basicAuth",
                };
                break;

            case "apikey":
                var apiKeyName = "X-API-Key";
                if (authElement.TryGetProperty("apikey", out var apikeyArray) && apikeyArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in apikeyArray.EnumerateArray())
                    {
                        if (GetStringProperty(item, "key") == "key")
                        {
                            apiKeyName = GetStringProperty(item, "value") ?? apiKeyName;
                        }
                    }
                }

                scheme = new ParsedSecurityScheme
                {
                    Name = "apiKeyAuth",
                    SchemeType = SchemeType.ApiKey,
                    ParameterName = apiKeyName,
                    ApiKeyLocation = Entities.ApiKeyLocation.Header,
                };
                requirement = new ParsedSecurityRequirement
                {
                    SecurityType = SecurityType.ApiKey,
                    SchemeName = "apiKeyAuth",
                };
                break;

            case "oauth2":
                scheme = new ParsedSecurityScheme
                {
                    Name = "oauth2Auth",
                    SchemeType = SchemeType.OAuth2,
                };

                if (authElement.TryGetProperty("oauth2", out var oauth2Array) && oauth2Array.ValueKind == JsonValueKind.Array)
                {
                    var configDict = new Dictionary<string, string>();
                    foreach (var item in oauth2Array.EnumerateArray())
                    {
                        var key = GetStringProperty(item, "key");
                        var value = GetStringProperty(item, "value");
                        if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                        {
                            configDict[key] = value;
                        }
                    }

                    if (configDict.Count > 0)
                    {
                        scheme.Configuration = JsonSerializer.Serialize(configDict);
                    }
                }

                requirement = new ParsedSecurityRequirement
                {
                    SecurityType = SecurityType.OAuth2,
                    SchemeName = "oauth2Auth",
                };
                break;

            default:
                return (null, null);
        }

        return (scheme, requirement);
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static string Slugify(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var slug = SlugifyRegex.Replace(text.Trim(), "_").Trim('_');
        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }
}
