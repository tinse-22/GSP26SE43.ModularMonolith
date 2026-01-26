using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Aspire ServiceDefaults extensions for the ClassifiedAds modular monolith.
/// Provides OpenTelemetry, health checks, and resilience defaults.
/// 
/// These extensions are automatically activated when running under .NET Aspire orchestration
/// and provide standardized observability, service discovery, and resilience patterns.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds Aspire service defaults to the host application builder.
    /// This includes OpenTelemetry (traces, metrics, logs) and service discovery.
    /// 
    /// Usage in host Program.cs:
    ///   var builder = Host.CreateApplicationBuilder(args);
    ///   builder.AddServiceDefaults(); // Adds telemetry, health checks, service discovery
    /// </summary>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        
        // Service discovery (automatic DNS/URL resolution for inter-service communication)
        builder.Services.AddServiceDiscovery();
        
        // Configure all HTTP clients with service discovery and resilience
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Enable service discovery for all HTTP clients by default
            http.AddServiceDiscovery();

            // Enable resilience with standard retry and circuit breaker policies
            http.AddStandardResilienceHandler();
        });

        return builder;
    }

    /// <summary>
    /// Configures OpenTelemetry for distributed tracing, metrics, and logging.
    /// Integrates with the existing Serilog/MEL configuration without breaking it.
    /// 
    /// When running under Aspire, telemetry is sent to Aspire Dashboard (OTLP endpoint).
    /// When running standalone, telemetry is sent to configured OTLP endpoint or console.
    /// </summary>
    public static IHostApplicationBuilder ConfigureOpenTelemetry(this IHostApplicationBuilder builder)
    {
        // Only enable if not already configured by the application
        // This preserves existing OpenTelemetry settings in appsettings
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()   // HTTP request metrics
                    .AddHttpClientInstrumentation()   // HTTP client metrics
                    .AddRuntimeInstrumentation();     // .NET runtime metrics (GC, threads, etc.)
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()   // HTTP request traces
                    .AddHttpClientInstrumentation();  // HTTP client traces
                    // Note: EF Core instrumentation added by modules via AddMonitoringServices
            });

        // Add OTLP exporters if available (Aspire dashboard or other collector)
        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static IHostApplicationBuilder AddOpenTelemetryExporters(this IHostApplicationBuilder builder)
    {
        // Check if OTLP endpoint is configured (either via Aspire or manual configuration)
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry()
                .UseOtlpExporter(); // Sends to OTEL_EXPORTER_OTLP_ENDPOINT (Aspire dashboard by default)
        }

        return builder;
    }

    /// <summary>
    /// Adds default health checks for the application.
    /// Provides /health (all checks) and /alive (liveness only) endpoints.
    /// </summary>
    public static IHostApplicationBuilder AddDefaultHealthChecks(this IHostApplicationBuilder builder)
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps default health check endpoints for the application.
    /// Call this in WebApplication pipeline configuration.
    /// 
    /// Endpoints:
    /// - GET /health - Runs all registered health checks (readiness)
    /// - GET /alive  - Runs only "live" tagged checks (liveness for orchestrators)
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Health checks endpoints
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        return app;
    }
}
