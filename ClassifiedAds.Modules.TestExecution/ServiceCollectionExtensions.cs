using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.ConfigurationOptions;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Persistence;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class TestExecutionServiceCollectionExtensions
{
    public static IServiceCollection AddTestExecutionModule(this IServiceCollection services, Action<TestExecutionModuleOptions> configureOptions)
    {
        var settings = new TestExecutionModuleOptions();
        configureOptions(settings);

        services.Configure(configureOptions);

        services.AddDbContext<TestExecutionDbContext>(options => options.UseNpgsql(settings.ConnectionStrings.Default, sql =>
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
            .AddScoped<IRepository<ExecutionEnvironment, Guid>, Repository<ExecutionEnvironment, Guid>>()
            .AddScoped<IRepository<TestRun, Guid>, Repository<TestRun, Guid>>()
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>();

        services.AddScoped<IExecutionAuthConfigService, ExecutionAuthConfigService>();

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
