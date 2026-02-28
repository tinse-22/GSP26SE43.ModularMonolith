using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Persistence;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class TestGenerationServiceCollectionExtensions
{
    public static IServiceCollection AddTestGenerationModule(this IServiceCollection services, Action<TestGenerationModuleOptions> configureOptions)
    {
        var settings = new TestGenerationModuleOptions();
        configureOptions(settings);

        services.Configure(configureOptions);

        services.AddDbContext<TestGenerationDbContext>(options => options.UseNpgsql(settings.ConnectionStrings.Default, sql =>
        {
            if (!string.IsNullOrEmpty(settings.ConnectionStrings.MigrationsAssembly))
            {
                sql.MigrationsAssembly(settings.ConnectionStrings.MigrationsAssembly);
            }

            if (settings.ConnectionStrings.CommandTimeout.HasValue)
            {
                sql.CommandTimeout(settings.ConnectionStrings.CommandTimeout);
            }
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
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>();

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
            .AddScoped<ITestSuiteScopeService, TestSuiteScopeService>();

        // FE-05B: Happy-path test case generation services
        services
            .AddScoped<ITestCaseRequestBuilder, TestCaseRequestBuilder>()
            .AddScoped<ITestCaseExpectationBuilder, TestCaseExpectationBuilder>()
            .AddScoped<IHappyPathTestCaseGenerator, HappyPathTestCaseGenerator>();

        // n8n Integration (typed HttpClient + Options pattern, same as PayOS in Subscription module)
        services.Configure<N8nIntegrationOptions>(options =>
        {
            var n8n = settings.N8nIntegration ?? new N8nIntegrationOptions();
            options.BaseUrl = n8n.BaseUrl ?? string.Empty;
            options.ApiKey = n8n.ApiKey ?? string.Empty;
            options.TimeoutSeconds = n8n.TimeoutSeconds <= 0 ? 30 : n8n.TimeoutSeconds;
            options.Webhooks = n8n.Webhooks ?? new System.Collections.Generic.Dictionary<string, string>();
        });

        services.AddHttpClient<IN8nIntegrationService, N8nIntegrationService>((sp, client) =>
        {
            var n8n = settings.N8nIntegration ?? new N8nIntegrationOptions();
            if (!string.IsNullOrWhiteSpace(n8n.BaseUrl))
            {
                client.BaseAddress = new System.Uri(n8n.BaseUrl);
            }

            client.Timeout = System.TimeSpan.FromSeconds(
                n8n.TimeoutSeconds > 0 ? n8n.TimeoutSeconds : 30);
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

        return services;
    }
}
