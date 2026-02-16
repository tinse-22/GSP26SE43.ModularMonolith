using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.ConfigurationOptions;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.HostedServices;
using ClassifiedAds.Modules.ApiDocumentation.Persistence;
using ClassifiedAds.Modules.ApiDocumentation.RateLimiterPolicies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class ApiDocumentationServiceCollectionExtensions
{
    public static IServiceCollection AddApiDocumentationModule(this IServiceCollection services, Action<ApiDocumentationModuleOptions> configureOptions)
    {
        var settings = new ApiDocumentationModuleOptions();
        configureOptions(settings);

        services.Configure(configureOptions);

        services.AddDbContext<ApiDocumentationDbContext>(options => options.UseNpgsql(settings.ConnectionStrings.Default, sql =>
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
            .AddScoped<IRepository<Project, Guid>, Repository<Project, Guid>>()
            .AddScoped<IRepository<ApiSpecification, Guid>, Repository<ApiSpecification, Guid>>()
            .AddScoped<IRepository<ApiEndpoint, Guid>, Repository<ApiEndpoint, Guid>>()
            .AddScoped<IRepository<EndpointParameter, Guid>, Repository<EndpointParameter, Guid>>()
            .AddScoped<IRepository<EndpointResponse, Guid>, Repository<EndpointResponse, Guid>>()
            .AddScoped<IRepository<EndpointSecurityReq, Guid>, Repository<EndpointSecurityReq, Guid>>()
            .AddScoped<IRepository<SecurityScheme, Guid>, Repository<SecurityScheme, Guid>>()
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>();

        services.AddMessageHandlers(Assembly.GetExecutingAssembly());
        services.AddAuthorizationPolicies(Assembly.GetExecutingAssembly());

        services.AddRateLimiter(options =>
        {
            options.AddPolicy<string, DefaultRateLimiterPolicy>(RateLimiterPolicyNames.DefaultPolicy);
        });

        return services;
    }

    public static IMvcBuilder AddApiDocumentationModule(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public static void MigrateApiDocumentationDb(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<ApiDocumentationDbContext>().Database.Migrate();
    }

    public static void MigrateApiDocumentationDb(this IHost app)
    {
        using var serviceScope = app.Services.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<ApiDocumentationDbContext>().Database.Migrate();
    }

    public static IServiceCollection AddHostedServicesApiDocumentationModule(this IServiceCollection services)
    {
        services.AddMessageBusConsumers(Assembly.GetExecutingAssembly());
        services.AddOutboxMessagePublishers(Assembly.GetExecutingAssembly());

        services.AddHostedService<PublishEventWorker>();

        return services;
    }
}
