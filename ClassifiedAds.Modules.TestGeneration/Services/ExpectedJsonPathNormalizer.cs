using ClassifiedAds.Contracts.TestExecution.JsonPathResolution;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Modules.TestGeneration.Services;

internal static class ExpectedJsonPathNormalizer
{
    public static Dictionary<string, string> NormalizeForEndpoint(
        IReadOnlyDictionary<string, string> jsonPathChecks,
        string endpointPath,
        IJsonPathResolver jsonPathResolver,
        IReadOnlyCollection<string> swaggerResponseSchemas = null,
        string actualResponseJson = null,
        string httpMethod = null)
    {
        if (jsonPathResolver == null)
        {
            throw new ArgumentNullException(nameof(jsonPathResolver));
        }

        if (jsonPathChecks == null || jsonPathChecks.Count == 0)
        {
            return jsonPathChecks == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(jsonPathChecks, StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var check in jsonPathChecks)
        {
            if (string.IsNullOrWhiteSpace(check.Key))
            {
                continue;
            }

            var resolution = jsonPathResolver.Resolve(new JsonPathResolutionRequest
            {
                OriginalPath = check.Key,
                ActualResponseJson = actualResponseJson,
                SwaggerResponseSchemas = swaggerResponseSchemas ?? Array.Empty<string>(),
                EndpointPath = endpointPath,
                HttpMethod = httpMethod,
            });

            var key = resolution.IsResolved && !string.IsNullOrWhiteSpace(resolution.ResolvedPath)
                ? resolution.ResolvedPath
                : check.Key.Trim();

            result[key] = check.Value;
        }

        return result
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }
}
