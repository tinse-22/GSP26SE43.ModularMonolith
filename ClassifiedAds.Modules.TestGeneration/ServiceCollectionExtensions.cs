using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Infrastructure.HostedServices;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.MessageBusConsumers;
using ClassifiedAds.Modules.TestGeneration.MessageBusMessages;
using ClassifiedAds.Modules.TestGeneration.Persistence;
using ClassifiedAds.Modules.TestGeneration.Services;
using ClassifiedAds.Persistence.PostgreSQL;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class TestGenerationServiceCollectionExtensions
{
    public static IServiceCollection AddTestGenerationModule(this IServiceCollection services, Action<TestGenerationModuleOptions> configureOptions)
    {
        var settings = new TestGenerationModuleOptions();
        configureOptions(settings);
        var connectionString = PostgresConnectionStringNormalizer.NormalizeForSupabasePooler(settings.ConnectionStrings.Default);

        services.Configure(configureOptions);

        services.AddDbContext<TestGenerationDbContext>(options => options.UseNpgsql(connectionString, sql =>
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

        // Register repositories
        services
            .AddScoped<IRepository<TestSuite, Guid>, Repository<TestSuite, Guid>>()
            .AddScoped<IRepository<TestOrderProposal, Guid>, Repository<TestOrderProposal, Guid>>()
            .AddScoped<IRepository<TestSuiteVersion, Guid>, Repository<TestSuiteVersion, Guid>>()
            .AddScoped<IRepository<TestCase, Guid>, Repository<TestCase, Guid>>()
            .AddScoped<IRepository<TestCaseDependency, Guid>, Repository<TestCaseDependency, Guid>>()
            .AddScoped<IRepository<TestCaseRequest, Guid>, Repository<TestCaseRequest, Guid>>()
            .AddScoped<IRepository<TestCaseExpectation, Guid>, Repository<TestCaseExpectation, Guid>>()
            .AddScoped<IRepository<TestCaseVariable, Guid>, Repository<TestCaseVariable, Guid>>()
            .AddScoped<IRepository<TestDataSet, Guid>, Repository<TestDataSet, Guid>>()
            .AddScoped<IRepository<TestCaseChangeLog, Guid>, Repository<TestCaseChangeLog, Guid>>()
            .AddScoped<IRepository<LlmSuggestion, Guid>, Repository<LlmSuggestion, Guid>>()
            .AddScoped<IRepository<LlmSuggestionFeedback, Guid>, Repository<LlmSuggestionFeedback, Guid>>()
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>()
            .AddScoped<IRepository<TestGenerationJob, Guid>, Repository<TestGenerationJob, Guid>>()
            // FE-18: SRS repositories
            .AddScoped<IRepository<SrsDocument, Guid>, Repository<SrsDocument, Guid>>()
            .AddScoped<IRepository<SrsRequirement, Guid>, Repository<SrsRequirement, Guid>>()
            .AddScoped<IRepository<SrsAnalysisJob, Guid>, Repository<SrsAnalysisJob, Guid>>()
            .AddScoped<IRepository<SrsRequirementClarification, Guid>, Repository<SrsRequirementClarification, Guid>>()
            .AddScoped<IRepository<TestCaseRequirementLink, Guid>, Repository<TestCaseRequirementLink, Guid>>();

        // Register paper-based algorithms (standalone, reusable, no DB dependency)
        services
            .AddSingleton<ISchemaRelationshipAnalyzer, SchemaRelationshipAnalyzer>()
            .AddSingleton<ISemanticTokenMatcher, SemanticTokenMatcher>()
            .AddSingleton<IDependencyAwareTopologicalSorter, DependencyAwareTopologicalSorter>()
            .AddSingleton<IObservationConfirmationPromptBuilder, ObservationConfirmationPromptBuilder>();

        services
            .AddScoped<IApiTestOrderAlgorithm, ApiTestOrderAlgorithm>()
            .AddScoped<IApiTestOrderService, ApiTestOrderService>()
            .AddScoped<IApiTestOrderGateService, ApiTestOrderGateService>()
            .AddScoped<ITestSuiteScopeService, TestSuiteScopeService>()
            .AddScoped<ITestSuiteProjectService, TestSuiteProjectService>()
            .AddScoped<IExpectationResolver, ExpectationResolver>();

        // FE-07/08: Cross-module execution read gateway
        services
            .AddScoped<ITestExecutionReadGatewayService, TestExecutionReadGatewayService>();

        // FE-05B: Happy-path test case generation services
        services
            .AddScoped<ITestCaseRequestBuilder, TestCaseRequestBuilder>()
            .AddScoped<ITestCaseExpectationBuilder, TestCaseExpectationBuilder>()
            .AddScoped<IHappyPathTestCaseGenerator, HappyPathTestCaseGenerator>();

        // FE-06: Boundary/negative test case generation services
        services
            .AddSingleton<LlmSuggestionFeedbackMetrics>()
            .AddScoped<IBodyMutationEngine, BodyMutationEngine>()
            .AddScoped<ILlmSuggestionFeedbackUpsertService, LlmSuggestionFeedbackUpsertService>()
            .AddScoped<ILlmSuggestionFeedbackContextService, LlmSuggestionFeedbackContextService>()
            .AddScoped<ILlmScenarioSuggester, LlmScenarioSuggester>()
            .AddScoped<ILlmSuggestionReviewService, LlmSuggestionReviewService>()
            .AddScoped<ILlmSuggestionMaterializer, LlmSuggestionMaterializer>()
            .AddScoped<IBoundaryNegativeTestCaseGenerator, BoundaryNegativeTestCaseGenerator>();

        // FE-17: Test generation payload builder (reusable by command handler and background consumer)
        services
            .AddScoped<ITestGenerationPayloadBuilder, TestGenerationPayloadBuilder>();

        // n8n Integration (typed HttpClient + Options pattern, same as PayOS in Subscription module)
        services.Configure<N8nIntegrationOptions>(options =>
        {
            var n8n = settings.N8nIntegration ?? new N8nIntegrationOptions();
            options.BaseUrl = n8n.BaseUrl ?? string.Empty;
            options.ApiKey = n8n.ApiKey ?? string.Empty;
            options.TimeoutSeconds = n8n.TimeoutSeconds <= 0 ? 600 : n8n.TimeoutSeconds;
            options.LlmSuggestionTimeoutSeconds = n8n.LlmSuggestionTimeoutSeconds <= 0 ? 90 : n8n.LlmSuggestionTimeoutSeconds;
            options.Webhooks = n8n.Webhooks ?? new System.Collections.Generic.Dictionary<string, string>();
            options.BeBaseUrl = n8n.BeBaseUrl ?? string.Empty;
            options.CallbackApiKey = n8n.CallbackApiKey ?? string.Empty;
            options.UseDotnetIntegrationWorkflowForGeneration = n8n.UseDotnetIntegrationWorkflowForGeneration;
            options.GenerationModel = string.IsNullOrWhiteSpace(n8n.GenerationModel)
                ? "gpt-4.1-mini"
                : n8n.GenerationModel;
            options.GenerationMaxOutputTokens = n8n.GenerationMaxOutputTokens <= 0 ? 4096 : n8n.GenerationMaxOutputTokens;
            options.GenerationMinOutputTokens = n8n.GenerationMinOutputTokens <= 0 ? 2048 : n8n.GenerationMinOutputTokens;
            options.GenerationOutputTokensPerEndpoint = n8n.GenerationOutputTokensPerEndpoint <= 0 ? 768 : n8n.GenerationOutputTokensPerEndpoint;
            options.GenerationMaxSchemaPayloadCountPerKind = n8n.GenerationMaxSchemaPayloadCountPerKind < 0 ? 0 : n8n.GenerationMaxSchemaPayloadCountPerKind;
            options.GenerationMaxSchemaPayloadLength = n8n.GenerationMaxSchemaPayloadLength <= 0 ? 800 : n8n.GenerationMaxSchemaPayloadLength;
            options.GenerationMaxPromptLength = n8n.GenerationMaxPromptLength <= 0 ? 1200 : n8n.GenerationMaxPromptLength;
            options.GenerationMaxBusinessContextLength = n8n.GenerationMaxBusinessContextLength <= 0 ? 700 : n8n.GenerationMaxBusinessContextLength;
            options.GenerationMaxSrsRequirementCount = n8n.GenerationMaxSrsRequirementCount <= 0 ? 15 : n8n.GenerationMaxSrsRequirementCount;
            options.GenerationMaxSrsFieldLength = n8n.GenerationMaxSrsFieldLength <= 0 ? 500 : n8n.GenerationMaxSrsFieldLength;
        });

        var n8nTimeoutSeconds = settings.N8nIntegration?.TimeoutSeconds > 0
            ? settings.N8nIntegration.TimeoutSeconds
            : 600; // Default 10 minutes for LLM webhook calls

        // Keep timeout budgets consistent with configured TimeoutSeconds to avoid hidden policy caps.
        // Use the same 10-minute budget across attempt/total/client timeout.
        var effectiveAttemptTimeoutSeconds = n8nTimeoutSeconds;
        var effectiveTotalTimeoutSeconds = n8nTimeoutSeconds;

        // Named client to exclude from default resilience handler in ServiceDefaults
        const string n8nHttpClientName = "N8nIntegrationHttpClient";

        services.AddHttpClient<IN8nIntegrationService, N8nIntegrationService>(n8nHttpClientName, (sp, client) =>
        {
            var n8n = settings.N8nIntegration ?? new N8nIntegrationOptions();
            if (!string.IsNullOrWhiteSpace(n8n.BaseUrl))
            {
                client.BaseAddress = new System.Uri(n8n.BaseUrl);
            }

            // Keep HttpClient timeout aligned with resilience timeouts (10 minutes by default).
            client.Timeout = System.TimeSpan.FromSeconds(n8nTimeoutSeconds);
        })
        .AddStandardResilienceHandler(options =>
        {
            // Override standard resilience timeouts for LLM/n8n webhook calls.
            // Standard handler has 10s attempt timeout which is too short for LLM operations
            options.AttemptTimeout.Timeout = System.TimeSpan.FromSeconds(effectiveAttemptTimeoutSeconds);
            options.TotalRequestTimeout.Timeout = System.TimeSpan.FromSeconds(effectiveTotalTimeoutSeconds);
            options.CircuitBreaker.SamplingDuration = System.TimeSpan.FromSeconds(Math.Max(effectiveAttemptTimeoutSeconds * 2, 1200));

            // LLM webhooks are expensive POST calls. Polly requires MaxRetryAttempts >= 1,
            // so disable retry via ShouldHandle instead of using an invalid retry count.
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.ShouldHandle = static _ => PredicateResult.False();
            options.Retry.Delay = System.TimeSpan.FromSeconds(1);
            options.Retry.BackoffType = DelayBackoffType.Constant;
        });

        services.AddMessageHandlers(Assembly.GetExecutingAssembly());
        services.AddAuthorizationPolicies(Assembly.GetExecutingAssembly());

        return services;
    }

    public static IMvcBuilder AddTestGenerationModule(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public static void MigrateTestGenerationDb(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<TestGenerationDbContext>().Database.Migrate();
    }

    public static void MigrateTestGenerationDb(this IHost app)
    {
        using var serviceScope = app.Services.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<TestGenerationDbContext>().Database.Migrate();
    }

    public static IServiceCollection AddHostedServicesTestGenerationModule(this IServiceCollection services)
    {
        services.AddMessageBusConsumers(Assembly.GetExecutingAssembly());
        services.AddOutboxMessagePublishers(Assembly.GetExecutingAssembly());
        services.AddHostedService<MessageBusConsumerBackgroundService<TriggerTestGenerationConsumer, TriggerTestGenerationMessage>>();

        return services;
    }
}
