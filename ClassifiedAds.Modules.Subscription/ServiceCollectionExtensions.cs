using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Subscription.ConfigurationOptions;
using ClassifiedAds.Modules.Subscription.Entities;
using ClassifiedAds.Modules.Subscription.HostedServices;
using ClassifiedAds.Modules.Subscription.Persistence;
using ClassifiedAds.Modules.Subscription.RateLimiterPolicies;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class SubscriptionServiceCollectionExtensions
{
    public static IServiceCollection AddSubscriptionModule(this IServiceCollection services, Action<SubscriptionModuleOptions> configureOptions)
    {
        var settings = new SubscriptionModuleOptions();
        configureOptions(settings);
        settings.ConnectionStrings ??= new ConnectionStringsOptions();

        services.Configure(configureOptions);

        if (string.IsNullOrWhiteSpace(settings.ConnectionStrings.Default))
        {
            throw new InvalidOperationException("Chưa cấu hình chuỗi kết nối cho module Subscription.");
        }

        services.AddDbContext<SubscriptionDbContext>(options => options.UseNpgsql(settings.ConnectionStrings.Default, sql =>
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
            .AddScoped<IRepository<SubscriptionPlan, Guid>, Repository<SubscriptionPlan, Guid>>()
            .AddScoped<IRepository<PlanLimit, Guid>, Repository<PlanLimit, Guid>>()
            .AddScoped<IRepository<UserSubscription, Guid>, Repository<UserSubscription, Guid>>()
            .AddScoped<IRepository<SubscriptionHistory, Guid>, Repository<SubscriptionHistory, Guid>>()
            .AddScoped<IRepository<UsageTracking, Guid>, Repository<UsageTracking, Guid>>()
            .AddScoped<IRepository<PaymentTransaction, Guid>, Repository<PaymentTransaction, Guid>>()
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

    public static IMvcBuilder AddSubscriptionModule(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public static void MigrateSubscriptionDb(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<SubscriptionDbContext>().Database.Migrate();
    }

    public static void MigrateSubscriptionDb(this IHost app)
    {
        using var serviceScope = app.Services.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<SubscriptionDbContext>().Database.Migrate();
    }

    public static IServiceCollection AddHostedServicesSubscriptionModule(this IServiceCollection services)
    {
        services.AddMessageBusConsumers(Assembly.GetExecutingAssembly());
        services.AddOutboxMessagePublishers(Assembly.GetExecutingAssembly());

        services.AddHostedService<PublishEventWorker>();

        return services;
    }
}
