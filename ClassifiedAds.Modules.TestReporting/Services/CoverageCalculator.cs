using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Modules.TestReporting.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestReporting.Services;

public class CoverageCalculator : ICoverageCalculator
{
    private static readonly Regex VersionSegmentRegex = new Regex(@"^v\d+$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public CoverageMetricModel Calculate(
        TestRunReportContextDto context,
        IReadOnlyCollection<ApiEndpointMetadataDto> scopedEndpointMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var scopedEndpointIds = (context.OrderedEndpointIds ?? Array.Empty<Guid>())
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToArray();
        var scopedEndpointIdSet = scopedEndpointIds.ToHashSet();
        var testedEndpointIds = (context.Results ?? Array.Empty<ReportTestCaseResultDto>())
            .Where(x => x.EndpointId.HasValue
                && scopedEndpointIdSet.Contains(x.EndpointId.Value)
                && !string.Equals(x.Status, "Skipped", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.EndpointId.Value)
            .Distinct()
            .ToHashSet();
        var descriptors = BuildDescriptors(context, scopedEndpointMetadata, scopedEndpointIds);

        return new CoverageMetricModel
        {
            TestRunId = context.Run?.TestRunId ?? Guid.Empty,
            TotalEndpoints = scopedEndpointIds.Length,
            TestedEndpoints = testedEndpointIds.Count,
            CoveragePercent = CalculatePercent(testedEndpointIds.Count, scopedEndpointIds.Length),
            ByMethod = BuildByMethod(descriptors, testedEndpointIds),
            ByTag = BuildByTag(descriptors, testedEndpointIds),
            UncoveredPaths = descriptors
                .Where(x => !testedEndpointIds.Contains(x.EndpointId))
                .Select(FormatEndpointDisplay)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            CalculatedAt = DateTimeOffset.UtcNow,
        };
    }

    private static IReadOnlyList<ScopedEndpointDescriptor> BuildDescriptors(
        TestRunReportContextDto context,
        IReadOnlyCollection<ApiEndpointMetadataDto> scopedEndpointMetadata,
        IReadOnlyCollection<Guid> scopedEndpointIds)
    {
        var descriptors = new Dictionary<Guid, ScopedEndpointDescriptor>();

        foreach (var metadata in scopedEndpointMetadata ?? Array.Empty<ApiEndpointMetadataDto>())
        {
            if (metadata == null || metadata.EndpointId == Guid.Empty)
            {
                continue;
            }

            descriptors[metadata.EndpointId] = new ScopedEndpointDescriptor
            {
                EndpointId = metadata.EndpointId,
                Method = NormalizeMethod(metadata.HttpMethod),
                Path = NormalizePath(metadata.Path),
                Tags = ResolveTags(metadata.Path),
            };
        }

        foreach (var definition in context.Definitions ?? Array.Empty<ReportTestCaseDefinitionDto>())
        {
            if (!definition.EndpointId.HasValue || definition.EndpointId.Value == Guid.Empty || descriptors.ContainsKey(definition.EndpointId.Value))
            {
                continue;
            }

            descriptors[definition.EndpointId.Value] = new ScopedEndpointDescriptor
            {
                EndpointId = definition.EndpointId.Value,
                Method = NormalizeMethod(definition.Request?.HttpMethod),
                Path = NormalizePath(definition.Request?.Url),
                Tags = ResolveTags(definition.Request?.Url),
            };
        }

        foreach (var result in context.Results ?? Array.Empty<ReportTestCaseResultDto>())
        {
            if (!result.EndpointId.HasValue || result.EndpointId.Value == Guid.Empty || descriptors.ContainsKey(result.EndpointId.Value))
            {
                continue;
            }

            descriptors[result.EndpointId.Value] = new ScopedEndpointDescriptor
            {
                EndpointId = result.EndpointId.Value,
                Method = "UNKNOWN",
                Path = NormalizePath(result.ResolvedUrl),
                Tags = ResolveTags(result.ResolvedUrl),
            };
        }

        return scopedEndpointIds
            .Select(endpointId => descriptors.TryGetValue(endpointId, out var descriptor)
                ? descriptor
                : new ScopedEndpointDescriptor
                {
                    EndpointId = endpointId,
                    Method = "UNKNOWN",
                    Path = $"endpoint:{endpointId}",
                    Tags = new[] { "untagged" },
                })
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Method, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.EndpointId)
            .ToArray();
    }

    private static Dictionary<string, decimal> BuildByMethod(
        IReadOnlyCollection<ScopedEndpointDescriptor> descriptors,
        ISet<Guid> testedEndpointIds)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        foreach (var group in descriptors
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Method) ? "UNKNOWN" : x.Method, StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var total = group.Select(x => x.EndpointId).Distinct().Count();
            var tested = group.Count(x => testedEndpointIds.Contains(x.EndpointId));
            result[group.Key] = CalculatePercent(tested, total);
        }

        return result;
    }

    private static Dictionary<string, decimal> BuildByTag(
        IReadOnlyCollection<ScopedEndpointDescriptor> descriptors,
        ISet<Guid> testedEndpointIds)
    {
        var tagToEndpointIds = new Dictionary<string, HashSet<Guid>>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in descriptors)
        {
            foreach (var tag in descriptor.Tags ?? Array.Empty<string>())
            {
                var normalizedTag = string.IsNullOrWhiteSpace(tag) ? "untagged" : tag.Trim().ToLowerInvariant();
                if (!tagToEndpointIds.TryGetValue(normalizedTag, out var endpointIds))
                {
                    endpointIds = new HashSet<Guid>();
                    tagToEndpointIds[normalizedTag] = endpointIds;
                }

                endpointIds.Add(descriptor.EndpointId);
            }
        }

        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in tagToEndpointIds.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var total = group.Value.Count;
            var tested = group.Value.Count(testedEndpointIds.Contains);
            result[group.Key] = CalculatePercent(tested, total);
        }

        return result;
    }

    private static string NormalizeMethod(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "UNKNOWN"
            : value.Trim().ToUpperInvariant();
    }

    private static string FormatEndpointDisplay(ScopedEndpointDescriptor descriptor)
    {
        if (descriptor == null || string.IsNullOrWhiteSpace(descriptor.Path))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(descriptor.Method) || string.Equals(descriptor.Method, "UNKNOWN", StringComparison.OrdinalIgnoreCase)
            ? descriptor.Path
            : $"{descriptor.Method} {descriptor.Path}";
    }

    private static string NormalizePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri.AbsolutePath;
        }

        if (Uri.TryCreate(value, UriKind.Relative, out var relativeUri))
        {
            var relativeValue = relativeUri.OriginalString;
            var queryIndex = relativeValue.IndexOf('?', StringComparison.Ordinal);
            return queryIndex >= 0
                ? relativeValue[..queryIndex]
                : relativeValue;
        }

        var fallback = value.Trim();
        var fallbackQueryIndex = fallback.IndexOf('?', StringComparison.Ordinal);
        return fallbackQueryIndex >= 0
            ? fallback[..fallbackQueryIndex]
            : fallback;
    }

    private static IReadOnlyList<string> ResolveTags(string path)
    {
        var normalizedPath = NormalizePath(path);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return new[] { "untagged" };
        }

        var tag = normalizedPath
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Select(x => x.Trim('{', '}'))
            .FirstOrDefault(x =>
                !string.IsNullOrWhiteSpace(x)
                && !string.Equals(x, "api", StringComparison.OrdinalIgnoreCase)
                && !VersionSegmentRegex.IsMatch(x));

        return string.IsNullOrWhiteSpace(tag)
            ? new[] { "untagged" }
            : new[] { tag.ToLowerInvariant() };
    }

    private static decimal CalculatePercent(int tested, int total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return Math.Round((decimal)tested / total * 100m, 2, MidpointRounding.AwayFromZero);
    }

    private sealed class ScopedEndpointDescriptor
    {
        public Guid EndpointId { get; set; }

        public string Method { get; set; }

        public string Path { get; set; }

        public IReadOnlyList<string> Tags { get; set; } = new[] { "untagged" };
    }
}
