using ClassifiedAds.Contracts.LlmAssistant.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.LlmAssistant.ConfigurationOptions;
using ClassifiedAds.Modules.LlmAssistant.Entities;
using ClassifiedAds.Modules.LlmAssistant.Persistence;
using ClassifiedAds.Modules.LlmAssistant.Services;
using ClassifiedAds.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class LlmAssistantServiceCollectionExtensions
{
    public static IServiceCollection AddLlmAssistantModule(this IServiceCollection services, Action<LlmAssistantModuleOptions> configureOptions)
    {
        var settings = new LlmAssistantModuleOptions();
        configureOptions(settings);
        settings.FailureExplanation ??= new FailureExplanationOptions();
        var connectionString = PostgresConnectionStringNormalizer.NormalizeForSupabasePooler(settings.ConnectionStrings.Default);

        services.Configure(configureOptions);

        services.AddDbContext<LlmAssistantDbContext>(options => options.UseNpgsql(connectionString, sql =>
        {
            if (!string.IsNullOrEmpty(settings.ConnectionStrings.MigrationsAssembly))
            {
                sql.MigrationsAssembly(settings.ConnectionStrings.MigrationsAssembly);
            }

            if (settings.ConnectionStrings.CommandTimeout.HasValue)
            {
                sql.CommandTimeout(settings.ConnectionStrings.CommandTimeout);
            }

            // Supabase pooler safety: single-statement batches prevent connector state corruption.
            sql.MaxBatchSize(1);
            sql.UseSupabaseRetryPolicy();
        }));

        services
            .AddScoped<IRepository<LlmInteraction, Guid>, Repository<LlmInteraction, Guid>>()
            .AddScoped<IRepository<LlmSuggestionCache, Guid>, Repository<LlmSuggestionCache, Guid>>()
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>();

        // FE-06: Cross-module gateway for LLM interaction audit + caching
        services.AddScoped<ILlmAssistantGatewayService, LlmAssistantGatewayService>();
        services.AddSingleton<FailureExplanationMetrics>();
        services.AddScoped<ILlmFailureExplainer, LlmFailureExplainer>();
        services.AddScoped<IFailureExplanationFingerprintBuilder, FailureExplanationFingerprintBuilder>();
        services.AddScoped<IFailureExplanationSanitizer, FailureExplanationSanitizer>();
        services.AddScoped<IFailureExplanationPromptBuilder, FailureExplanationPromptBuilder>();
        services.AddScoped<ILlmFailureExplanationClient, N8nFailureExplanationClient>();

        services.AddHttpClient(N8nFailureExplanationClient.HttpClientName, client =>
        {
            if (!string.IsNullOrWhiteSpace(settings.FailureExplanation.BaseUrl))
            {
                client.BaseAddress = new Uri(settings.FailureExplanation.BaseUrl, UriKind.Absolute);
            }

            client.Timeout = TimeSpan.FromSeconds(
                settings.FailureExplanation.TimeoutSeconds > 0
                    ? settings.FailureExplanation.TimeoutSeconds
                    : 30);
        });

        services.AddMessageHandlers(Assembly.GetExecutingAssembly());

        return services;
    }

    public static IMvcBuilder AddLlmAssistantModule(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public static void MigrateLlmAssistantDb(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<LlmAssistantDbContext>().Database.Migrate();
    }

    public static void MigrateLlmAssistantDb(this IHost app)
    {
        using var serviceScope = app.Services.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<LlmAssistantDbContext>().Database.Migrate();
    }
}
