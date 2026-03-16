using System.Diagnostics.Metrics;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public sealed class FailureExplanationMetrics
{
    public const string MeterName = "ClassifiedAds.LlmAssistant.FailureExplanation";

    private readonly Counter<long> _requestsTotal;
    private readonly Counter<long> _cacheHitTotal;
    private readonly Counter<long> _cacheMissTotal;
    private readonly Counter<long> _failuresTotal;
    private readonly Histogram<double> _latencyMs;

    public FailureExplanationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _requestsTotal = meter.CreateCounter<long>(
            "llm_failure_explanation_requests_total",
            description: "Total number of failure explanation requests.");

        _cacheHitTotal = meter.CreateCounter<long>(
            "llm_failure_explanation_cache_hit_total",
            description: "Total number of cache hits for failure explanations.");

        _cacheMissTotal = meter.CreateCounter<long>(
            "llm_failure_explanation_cache_miss_total",
            description: "Total number of cache misses for failure explanations.");

        _failuresTotal = meter.CreateCounter<long>(
            "llm_failure_explanation_failures_total",
            description: "Total number of failed failure explanation attempts.");

        _latencyMs = meter.CreateHistogram<double>(
            "llm_failure_explanation_latency_ms",
            unit: "ms",
            description: "Latency of live failure explanation generation in milliseconds.");
    }

    public void RecordRequest() => _requestsTotal.Add(1);

    public void RecordCacheHit() => _cacheHitTotal.Add(1);

    public void RecordCacheMiss() => _cacheMissTotal.Add(1);

    public void RecordFailure() => _failuresTotal.Add(1);

    public void RecordLatency(double milliseconds) => _latencyMs.Record(milliseconds);
}
