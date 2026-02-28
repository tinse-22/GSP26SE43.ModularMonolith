using ClassifiedAds.Modules.ApiDocumentation.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.ApiDocumentation.Services;

public class OpenApiSpecificationParser : ISpecificationParser
{
    private static readonly HashSet<string> ValidHttpMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "get", "post", "put", "delete", "patch", "head", "options", "trace",
    };

    private static readonly Dictionary<string, string> LocationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["path"] = "Path",
        ["query"] = "Query",
        ["header"] = "Header",
        ["cookie"] = "Cookie",
        ["body"] = "Body",
    };

    public bool CanParse(SourceType sourceType)
    {
        return sourceType == SourceType.OpenAPI;
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

            // Detect OpenAPI version
            bool isSwagger2 = root.TryGetProperty("swagger", out var swaggerProp);
            bool isOpenApi3 = root.TryGetProperty("openapi", out var openapiProp);

            if (!isSwagger2 && !isOpenApi3)
            {
                result.Success = false;
                result.Errors.Add("Not a valid OpenAPI document: missing 'openapi' or 'swagger' property.");
                return Task.FromResult(result);
            }

            // Extract info version
            if (root.TryGetProperty("info", out var infoElement) &&
                infoElement.TryGetProperty("version", out var versionElement) &&
                versionElement.ValueKind == JsonValueKind.String)
            {
                result.DetectedVersion = versionElement.GetString();
            }

            // Parse security schemes
            result.SecuritySchemes = ParseSecuritySchemes(root, isSwagger2);
            var securitySchemeTypeMap = BuildSecuritySchemeTypeMap(result.SecuritySchemes);

            // Parse paths
            if (root.TryGetProperty("paths", out var pathsElement) && pathsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var pathItem in pathsElement.EnumerateObject())
                {
                    var path = pathItem.Name;
                    if (pathItem.Value.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    // Collect path-level parameters
                    List<JsonElement> pathLevelParameters = null;
                    if (pathItem.Value.TryGetProperty("parameters", out var pathParamsElement) &&
                        pathParamsElement.ValueKind == JsonValueKind.Array)
                    {
                        pathLevelParameters = pathParamsElement.EnumerateArray().ToList();
                    }

                    foreach (var operation in pathItem.Value.EnumerateObject())
                    {
                        if (!ValidHttpMethods.Contains(operation.Name) ||
                            operation.Value.ValueKind != JsonValueKind.Object)
                        {
                            continue;
                        }

                        var endpoint = ParseOperation(path, operation.Name.ToUpperInvariant(), operation.Value, pathLevelParameters, securitySchemeTypeMap, isSwagger2);
                        result.Endpoints.Add(endpoint);
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
            result.Errors.Add($"Failed to parse OpenAPI document: {ex.Message}");
        }

        return Task.FromResult(result);
    }

    private static ParsedEndpoint ParseOperation(
        string path,
        string httpMethod,
        JsonElement operation,
        List<JsonElement> pathLevelParameters,
        Dictionary<string, SecurityType> securitySchemeTypeMap,
        bool isSwagger2)
    {
        var endpoint = new ParsedEndpoint
        {
            HttpMethod = httpMethod,
            Path = path,
            OperationId = GetStringProperty(operation, "operationId"),
            Summary = GetStringProperty(operation, "summary"),
            Description = GetStringProperty(operation, "description"),
            IsDeprecated = GetBoolProperty(operation, "deprecated"),
        };

        // Tags
        if (operation.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var tag in tagsElement.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String)
                {
                    var tagName = tag.GetString();
                    if (!string.IsNullOrWhiteSpace(tagName))
                    {
                        endpoint.Tags.Add(tagName);
                    }
                }
            }
        }

        // Merge path-level parameters with operation-level (operation overrides)
        var mergedParams = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

        if (pathLevelParameters != null)
        {
            foreach (var param in pathLevelParameters)
            {
                var key = $"{GetStringProperty(param, "in")}:{GetStringProperty(param, "name")}";
                mergedParams[key] = param;
            }
        }

        if (operation.TryGetProperty("parameters", out var paramsElement) && paramsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var param in paramsElement.EnumerateArray())
            {
                var key = $"{GetStringProperty(param, "in")}:{GetStringProperty(param, "name")}";
                mergedParams[key] = param;
            }
        }

        foreach (var param in mergedParams.Values)
        {
            var mappedParam = MapParameter(param, isSwagger2);
            if (mappedParam != null)
            {
                endpoint.Parameters.Add(mappedParam);
            }
        }

        // Request body (OpenAPI 3.x)
        if (!isSwagger2 && operation.TryGetProperty("requestBody", out var requestBodyElement))
        {
            var bodyParam = MapRequestBody(requestBodyElement);
            if (bodyParam != null)
            {
                endpoint.Parameters.Add(bodyParam);
            }
        }

        // Responses
        if (operation.TryGetProperty("responses", out var responsesElement) && responsesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var response in responsesElement.EnumerateObject())
            {
                if (response.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                endpoint.Responses.Add(MapResponse(response.Name, response.Value));
            }
        }

        // Security requirements
        if (operation.TryGetProperty("security", out var securityElement) && securityElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var secReq in securityElement.EnumerateArray())
            {
                if (secReq.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                foreach (var scheme in secReq.EnumerateObject())
                {
                    var schemeName = scheme.Name;
                    if (string.IsNullOrWhiteSpace(schemeName))
                    {
                        continue;
                    }

                    var securityType = securitySchemeTypeMap.TryGetValue(schemeName, out var st)
                        ? st
                        : SecurityType.Bearer;

                    string scopes = null;
                    if (scheme.Value.ValueKind == JsonValueKind.Array && scheme.Value.GetArrayLength() > 0)
                    {
                        scopes = scheme.Value.GetRawText();
                    }

                    endpoint.SecurityRequirements.Add(new ParsedSecurityRequirement
                    {
                        SecurityType = securityType,
                        SchemeName = schemeName,
                        Scopes = scopes,
                    });
                }
            }
        }

        return endpoint;
    }

    private static ParsedParameter MapParameter(JsonElement param, bool isSwagger2)
    {
        var name = GetStringProperty(param, "name");
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var inValue = GetStringProperty(param, "in") ?? "query";
        var location = LocationMap.TryGetValue(inValue, out var mapped) ? mapped : "Query";

        // In Swagger 2.0, body parameters have "in": "body" with a "schema" at param level
        if (isSwagger2 && string.Equals(inValue, "body", StringComparison.OrdinalIgnoreCase))
        {
            string schema = null;
            if (param.TryGetProperty("schema", out var schemaElement))
            {
                schema = schemaElement.GetRawText();
            }

            return new ParsedParameter
            {
                Name = "body",
                Location = "Body",
                DataType = "object",
                IsRequired = GetBoolProperty(param, "required"),
                Schema = schema,
            };
        }

        string dataType = "string";
        string format = null;
        string schemaJson = null;
        string defaultValue = null;

        // OpenAPI 3.x uses "schema" object on the parameter
        if (param.TryGetProperty("schema", out var schemaEl) && schemaEl.ValueKind == JsonValueKind.Object)
        {
            dataType = GetStringProperty(schemaEl, "type") ?? "string";
            format = GetStringProperty(schemaEl, "format");
            schemaJson = schemaEl.GetRawText();

            if (schemaEl.TryGetProperty("default", out var defaultEl))
            {
                defaultValue = defaultEl.ValueKind == JsonValueKind.String
                    ? defaultEl.GetString()
                    : defaultEl.GetRawText();
            }
        }
        else
        {
            // Swagger 2.0: type/format are at parameter level
            dataType = GetStringProperty(param, "type") ?? "string";
            format = GetStringProperty(param, "format");
        }

        string examplesJson = null;
        if (param.TryGetProperty("example", out var exampleEl))
        {
            examplesJson = exampleEl.ValueKind == JsonValueKind.String
                ? exampleEl.GetString()
                : exampleEl.GetRawText();
        }
        else if (param.TryGetProperty("examples", out var examplesEl) && examplesEl.ValueKind == JsonValueKind.Object)
        {
            examplesJson = examplesEl.GetRawText();
        }

        return new ParsedParameter
        {
            Name = name,
            Location = location,
            DataType = dataType,
            Format = format,
            IsRequired = GetBoolProperty(param, "required"),
            DefaultValue = defaultValue,
            Schema = schemaJson,
            Examples = examplesJson,
        };
    }

    private static ParsedParameter MapRequestBody(JsonElement requestBody)
    {
        if (requestBody.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!requestBody.TryGetProperty("content", out var contentElement) ||
            contentElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        // Prefer application/json
        JsonElement mediaType;
        if (contentElement.TryGetProperty("application/json", out mediaType))
        {
            // use it
        }
        else
        {
            // Take the first content type
            using var enumerator = contentElement.EnumerateObject();
            if (!enumerator.MoveNext())
            {
                return null;
            }

            mediaType = enumerator.Current.Value;
        }

        string schemaJson = null;
        string examplesJson = null;

        if (mediaType.TryGetProperty("schema", out var schemaEl))
        {
            schemaJson = schemaEl.GetRawText();
        }

        if (mediaType.TryGetProperty("example", out var exampleEl))
        {
            examplesJson = exampleEl.ValueKind == JsonValueKind.String
                ? exampleEl.GetString()
                : exampleEl.GetRawText();
        }
        else if (mediaType.TryGetProperty("examples", out var examplesEl) && examplesEl.ValueKind == JsonValueKind.Object)
        {
            examplesJson = examplesEl.GetRawText();
        }

        return new ParsedParameter
        {
            Name = "body",
            Location = "Body",
            DataType = "object",
            IsRequired = GetBoolProperty(requestBody, "required"),
            Schema = schemaJson,
            Examples = examplesJson,
        };
    }

    private static ParsedResponse MapResponse(string statusCodeKey, JsonElement response)
    {
        int statusCode;
        if (string.Equals(statusCodeKey, "default", StringComparison.OrdinalIgnoreCase))
        {
            statusCode = 0;
        }
        else if (!int.TryParse(statusCodeKey, out statusCode))
        {
            statusCode = 0;
        }

        string schemaJson = null;
        string examplesJson = null;
        string headersJson = null;

        // OpenAPI 3.x: responses have "content" with media types
        if (response.TryGetProperty("content", out var contentElement) && contentElement.ValueKind == JsonValueKind.Object)
        {
            JsonElement mediaType;
            if (contentElement.TryGetProperty("application/json", out mediaType))
            {
                // use it
            }
            else
            {
                using var enumerator = contentElement.EnumerateObject();
                if (enumerator.MoveNext())
                {
                    mediaType = enumerator.Current.Value;
                }
            }

            if (mediaType.ValueKind == JsonValueKind.Object)
            {
                if (mediaType.TryGetProperty("schema", out var schemaEl))
                {
                    schemaJson = schemaEl.GetRawText();
                }

                if (mediaType.TryGetProperty("example", out var exampleEl))
                {
                    examplesJson = exampleEl.ValueKind == JsonValueKind.String
                        ? exampleEl.GetString()
                        : exampleEl.GetRawText();
                }
                else if (mediaType.TryGetProperty("examples", out var examplesEl) && examplesEl.ValueKind == JsonValueKind.Object)
                {
                    examplesJson = examplesEl.GetRawText();
                }
            }
        }
        else if (response.TryGetProperty("schema", out var swagger2SchemaEl))
        {
            // Swagger 2.0: schema directly on response
            schemaJson = swagger2SchemaEl.GetRawText();
        }

        if (response.TryGetProperty("headers", out var headersElement) && headersElement.ValueKind == JsonValueKind.Object)
        {
            headersJson = headersElement.GetRawText();
        }

        return new ParsedResponse
        {
            StatusCode = statusCode,
            Description = GetStringProperty(response, "description"),
            Schema = schemaJson,
            Examples = examplesJson,
            Headers = headersJson,
        };
    }

    private static List<ParsedSecurityScheme> ParseSecuritySchemes(JsonElement root, bool isSwagger2)
    {
        var schemes = new List<ParsedSecurityScheme>();

        JsonElement schemesElement;

        if (isSwagger2)
        {
            // Swagger 2.0: securityDefinitions
            if (!root.TryGetProperty("securityDefinitions", out schemesElement) ||
                schemesElement.ValueKind != JsonValueKind.Object)
            {
                return schemes;
            }
        }
        else
        {
            // OpenAPI 3.x: components.securitySchemes
            if (!root.TryGetProperty("components", out var components) ||
                !components.TryGetProperty("securitySchemes", out schemesElement) ||
                schemesElement.ValueKind != JsonValueKind.Object)
            {
                return schemes;
            }
        }

        foreach (var kvp in schemesElement.EnumerateObject())
        {
            if (kvp.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var parsed = ParseSingleSecurityScheme(kvp.Name, kvp.Value, isSwagger2);
            if (parsed != null)
            {
                schemes.Add(parsed);
            }
        }

        return schemes;
    }

    private static ParsedSecurityScheme ParseSingleSecurityScheme(string name, JsonElement scheme, bool isSwagger2)
    {
        var type = GetStringProperty(scheme, "type")?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var parsed = new ParsedSecurityScheme { Name = name };

        switch (type)
        {
            case "http":
                parsed.SchemeType = SchemeType.Http;
                parsed.Scheme = GetStringProperty(scheme, "scheme");
                parsed.BearerFormat = GetStringProperty(scheme, "bearerFormat");
                break;

            case "apikey":
                parsed.SchemeType = SchemeType.ApiKey;
                parsed.ParameterName = GetStringProperty(scheme, "name");
                var inValue = GetStringProperty(scheme, "in")?.ToLowerInvariant();
                parsed.ApiKeyLocation = inValue switch
                {
                    "query" => ApiKeyLocation.Query,
                    "cookie" => ApiKeyLocation.Cookie,
                    _ => ApiKeyLocation.Header,
                };
                break;

            case "oauth2":
                parsed.SchemeType = SchemeType.OAuth2;
                if (scheme.TryGetProperty("flows", out var flowsEl))
                {
                    parsed.Configuration = flowsEl.GetRawText();
                }

                break;

            case "openidconnect":
                parsed.SchemeType = SchemeType.OpenIdConnect;
                parsed.Configuration = GetStringProperty(scheme, "openIdConnectUrl");
                break;

            case "basic":
                // Swagger 2.0 "basic" type
                parsed.SchemeType = SchemeType.Http;
                parsed.Scheme = "basic";
                break;

            default:
                return null;
        }

        return parsed;
    }

    private static Dictionary<string, SecurityType> BuildSecuritySchemeTypeMap(List<ParsedSecurityScheme> schemes)
    {
        var map = new Dictionary<string, SecurityType>(StringComparer.OrdinalIgnoreCase);

        foreach (var scheme in schemes)
        {
            var securityType = scheme.SchemeType switch
            {
                SchemeType.Http when string.Equals(scheme.Scheme, "basic", StringComparison.OrdinalIgnoreCase) => SecurityType.Basic,
                SchemeType.Http => SecurityType.Bearer,
                SchemeType.ApiKey => SecurityType.ApiKey,
                SchemeType.OAuth2 => SecurityType.OAuth2,
                SchemeType.OpenIdConnect => SecurityType.OpenIdConnect,
                _ => SecurityType.Bearer,
            };

            map[scheme.Name] = securityType;
        }

        return map;
    }

    private static string GetStringProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return null;
    }

    private static bool GetBoolProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
        {
            return prop.ValueKind == JsonValueKind.True;
        }

        return false;
    }
}
