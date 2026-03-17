using ClassifiedAds.Modules.TestReporting.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestReporting.Models;

public class CoverageMetricModel
{
    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Guid TestRunId { get; set; }

    public int TotalEndpoints { get; set; }

    public int TestedEndpoints { get; set; }

    public decimal CoveragePercent { get; set; }

    public Dictionary<string, decimal> ByMethod { get; set; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, decimal> ByTag { get; set; } = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

    public List<string> UncoveredPaths { get; set; } = new List<string>();

    public DateTimeOffset CalculatedAt { get; set; }

    public static CoverageMetricModel FromEntity(CoverageMetric entity)
    {
        if (entity == null)
        {
            return null;
        }

        return new CoverageMetricModel
        {
            TestRunId = entity.TestRunId,
            TotalEndpoints = entity.TotalEndpoints,
            TestedEndpoints = entity.TestedEndpoints,
            CoveragePercent = entity.CoveragePercent,
            ByMethod = DeserializeDictionary(entity.ByMethod),
            ByTag = DeserializeDictionary(entity.ByTag),
            UncoveredPaths = DeserializeList(entity.UncoveredPaths),
            CalculatedAt = entity.CalculatedAt,
        };
    }

    internal string SerializeByMethod()
    {
        return JsonSerializer.Serialize(NormalizeDictionary(ByMethod), JsonOptions);
    }

    internal string SerializeByTag()
    {
        return JsonSerializer.Serialize(NormalizeDictionary(ByTag), JsonOptions);
    }

    internal string SerializeUncoveredPaths()
    {
        return JsonSerializer.Serialize((UncoveredPaths ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList(), JsonOptions);
    }

    private static Dictionary<string, decimal> DeserializeDictionary(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        var deserialized = JsonSerializer.Deserialize<Dictionary<string, decimal>>(value, JsonOptions)
            ?? new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        return NormalizeDictionary(deserialized);
    }

    private static List<string> DeserializeList(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new List<string>();
        }

        return (JsonSerializer.Deserialize<List<string>>(value, JsonOptions) ?? new List<string>())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Dictionary<string, decimal> NormalizeDictionary(IDictionary<string, decimal> source)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (source == null)
        {
            return result;
        }

        foreach (var item in source
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            result[item.Key] = item.Value;
        }

        return result;
    }
}
