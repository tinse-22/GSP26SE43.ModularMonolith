using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.LlmAssistant.ConfigurationOptions;
using ClassifiedAds.Modules.LlmAssistant.Entities;
using ClassifiedAds.Modules.LlmAssistant.Persistence;
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

        services.Configure(configureOptions);

        services.AddDbContext<LlmAssistantDbContext>(options => options.UseNpgsql(settings.ConnectionStrings.Default, sql =>
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

        services
            .AddScoped<IRepository<LlmInteraction, Guid>, Repository<LlmInteraction, Guid>>()
            .AddScoped<IRepository<LlmSuggestionCache, Guid>, Repository<LlmSuggestionCache, Guid>>()
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>();

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
