using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestReporting.ConfigurationOptions;
using ClassifiedAds.Modules.TestReporting.Entities;
using ClassifiedAds.Modules.TestReporting.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class TestReportingServiceCollectionExtensions
{
    public static IServiceCollection AddTestReportingModule(this IServiceCollection services, Action<TestReportingModuleOptions> configureOptions)
    {
        var settings = new TestReportingModuleOptions();
        configureOptions(settings);

        services.Configure(configureOptions);

        services.AddDbContext<TestReportingDbContext>(options => options.UseNpgsql(settings.ConnectionStrings.Default, sql =>
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
            .AddScoped<IRepository<TestReport, Guid>, Repository<TestReport, Guid>>()
            .AddScoped<IRepository<CoverageMetric, Guid>, Repository<CoverageMetric, Guid>>()
            .AddScoped<IRepository<AuditLogEntry, Guid>, Repository<AuditLogEntry, Guid>>()
            .AddScoped<IRepository<OutboxMessage, Guid>, Repository<OutboxMessage, Guid>>();

        services.AddMessageHandlers(Assembly.GetExecutingAssembly());

        return services;
    }

    public static IMvcBuilder AddTestReportingModule(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public static void MigrateTestReportingDb(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<TestReportingDbContext>().Database.Migrate();
    }

    public static void MigrateTestReportingDb(this IHost app)
    {
        using var serviceScope = app.Services.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<TestReportingDbContext>().Database.Migrate();
    }
}
