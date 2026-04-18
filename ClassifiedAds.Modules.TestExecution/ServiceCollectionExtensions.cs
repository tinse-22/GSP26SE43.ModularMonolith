using ClassifiedAds.Contracts.TestExecution.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.ConfigurationOptions;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models.Validators;
using ClassifiedAds.Modules.TestExecution.Persistence;
using ClassifiedAds.Modules.TestExecution.Services;
using ClassifiedAds.Persistence.PostgreSQL;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Polly;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class TestExecutionServiceCollectionExtensions
{
    public static IServiceCollection AddTestExecutionModule(this IServiceCollection services, Action<TestExecutionModuleOptions> configureOptions)
    {
        var settings = new TestExecutionModuleOptions();
        configureOptions(settings);
        var connectionString = PostgresConnectionStringNormalizer.NormalizeForSupabasePooler(settings.ConnectionStrings.Default);

        services.Configure(configureOptions);

        services.AddDbContext<TestExecutionDbContext>(options => options.UseNpgsql(connectionString, sql =>
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
            .AddScoped<IRepository<ExecutionEnvironment, Guid>, Repository<ExecutionEnvironment, Guid>>()
            .AddScoped<IRepository<TestRun, Guid>, Repository<TestRun, Guid>>()
            .AddScoped<IRepository<TestCaseResult, Guid>, Repository<TestCaseResult, Guid>>()
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>();

        services.AddScoped<IExecutionAuthConfigService, ExecutionAuthConfigService>();
        services.AddScoped<ITestFailureReadGatewayService, TestFailureReadGatewayService>();
        services.AddScoped<ITestRunReportReadGatewayService, TestRunReportReadGatewayService>();
        services.AddValidatorsFromAssemblyContaining<StartTestRunRequestValidator>();

        // FE-07/08: Test Execution Engine + Rule-Based Validation
        services.AddScoped<ITestExecutionOrchestrator, TestExecutionOrchestrator>();
        services.AddScoped<IExecutionEnvironmentRuntimeResolver, ExecutionEnvironmentRuntimeResolver>();
        services.AddScoped<IVariableResolver, VariableResolver>();
        services.AddScoped<IPreExecutionValidator, PreExecutionValidator>();
        services.AddScoped<IHttpTestExecutor, HttpTestExecutor>();
        services.AddScoped<IVariableExtractor, VariableExtractor>();
        services.AddScoped<IRuleBasedValidator, RuleBasedValidator>();
        services.AddScoped<ITestResultCollector, TestResultCollector>();
        services.AddHttpClient("TestExecution")
            .ConfigurePrimaryHttpMessageHandler(() => new System.Net.Http.HttpClientHandler
            {
                // Allow self-signed certs for test environments
                ServerCertificateCustomValidationCallback =
                    System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
            })
            .AddStandardResilienceHandler(options =>
            {
                // Test execution timeout is controlled per-request via HttpClient.Timeout (request.TimeoutMs).
                // Default Polly AttemptTimeout is 10s — far too short for API tests (30s+ typical).
                options.AttemptTimeout.Timeout = System.TimeSpan.FromSeconds(120);
                options.TotalRequestTimeout.Timeout = System.TimeSpan.FromSeconds(180);
                options.CircuitBreaker.SamplingDuration = System.TimeSpan.FromSeconds(240);

                // Allow a single retry for transient connection errors (e.g. Render cold-start ResponseEnded).
                // ShouldHandle uses the default transient-error predicate (HttpRequestException, 5xx, 408).
                options.Retry.MaxRetryAttempts = 1;
                options.Retry.Delay = System.TimeSpan.FromSeconds(2);
                options.Retry.BackoffType = DelayBackoffType.Constant;
            });

        services.AddMessageHandlers(Assembly.GetExecutingAssembly());
        services.AddAuthorizationPolicies(Assembly.GetExecutingAssembly());

        return services;
    }

    public static IMvcBuilder AddTestExecutionModule(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public static void MigrateTestExecutionDb(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<TestExecutionDbContext>().Database.Migrate();
    }

    public static void MigrateTestExecutionDb(this IHost app)
    {
        using var serviceScope = app.Services.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<TestExecutionDbContext>().Database.Migrate();
    }
}
