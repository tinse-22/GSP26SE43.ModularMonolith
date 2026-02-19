using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
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
            .AddScoped<IRepository<TestCaseRequest, Guid>, Repository<TestCaseRequest, Guid>>()
            .AddScoped<IRepository<TestCaseExpectation, Guid>, Repository<TestCaseExpectation, Guid>>()
            .AddScoped<IRepository<TestCaseVariable, Guid>, Repository<TestCaseVariable, Guid>>()
            .AddScoped<IRepository<TestDataSet, Guid>, Repository<TestDataSet, Guid>>()
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>();

        services
            .AddScoped<IApiTestOrderService, ApiTestOrderService>()
            .AddScoped<IApiTestOrderGateService, ApiTestOrderGateService>();

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
