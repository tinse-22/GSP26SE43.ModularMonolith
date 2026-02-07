using ClassifiedAds.Contracts.Notification.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.Notification.ConfigurationOptions;
using ClassifiedAds.Modules.Notification.EmailQueue;
using ClassifiedAds.Modules.Notification.Entities;
using ClassifiedAds.Modules.Notification.HostedServices;
using ClassifiedAds.Modules.Notification.Persistence;
using ClassifiedAds.Modules.Notification.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services, Action<NotificationModuleOptions> configureOptions)
    {
        var settings = new NotificationModuleOptions();
        configureOptions(settings);

        services.Configure(configureOptions);

        services
            .AddDbContext<NotificationDbContext>(options => options.UseNpgsql(settings.ConnectionStrings.Default, sql =>
            {
                if (!string.IsNullOrEmpty(settings.ConnectionStrings.MigrationsAssembly))
                {
                    sql.MigrationsAssembly(settings.ConnectionStrings.MigrationsAssembly);
                }

                if (settings.ConnectionStrings.CommandTimeout.HasValue)
                {
                    sql.CommandTimeout(settings.ConnectionStrings.CommandTimeout);
                }
            }))
            .AddScoped<IRepository<EmailMessage, Guid>, Repository<EmailMessage, Guid>>()
            .AddScoped<IRepository<SmsMessage, Guid>, Repository<SmsMessage, Guid>>()
            .AddScoped(typeof(IEmailMessageRepository), typeof(EmailMessageRepository))
            .AddScoped(typeof(ISmsMessageRepository), typeof(SmsMessageRepository))
            .AddScoped<IEmailMessageService, EmailMessageService>()
            .AddSingleton<IEmailTemplateService, EmailTemplateService>();

        // ─── Email Queue (Channel-based async pipeline) ──────────────────
        services.Configure<EmailQueueOptions>(opt =>
        {
            settings.EmailQueue ??= new EmailQueueOptions();
            opt.ChannelCapacity = settings.EmailQueue.ChannelCapacity;
            opt.MaxDegreeOfParallelism = settings.EmailQueue.MaxDegreeOfParallelism;
            opt.MaxRetryAttempts = settings.EmailQueue.MaxRetryAttempts;
            opt.BaseDelaySeconds = settings.EmailQueue.BaseDelaySeconds;
            opt.MaxDelaySeconds = settings.EmailQueue.MaxDelaySeconds;
            opt.SendTimeoutSeconds = settings.EmailQueue.SendTimeoutSeconds;
            opt.CircuitBreakerFailureThreshold = settings.EmailQueue.CircuitBreakerFailureThreshold;
            opt.CircuitBreakerDurationSeconds = settings.EmailQueue.CircuitBreakerDurationSeconds;
            opt.DbSweepIntervalSeconds = settings.EmailQueue.DbSweepIntervalSeconds;
        });

        // Singleton channel — shared between producers (scoped services) and consumers (workers)
        services.AddSingleton<EmailChannel>();
        services.AddSingleton<IEmailQueueWriter>(sp => sp.GetRequiredService<EmailChannel>());
        services.AddSingleton<IEmailQueueReader>(sp => sp.GetRequiredService<EmailChannel>());

        services.AddMessageHandlers(Assembly.GetExecutingAssembly());

        services.AddNotificationServices(settings);

        return services;
    }

    public static IMvcBuilder AddNotificationModule(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(Assembly.GetExecutingAssembly());
    }

    public static void MigrateNotificationDb(this IApplicationBuilder app)
    {
        using var serviceScope = app.ApplicationServices.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.Migrate();
    }

    public static void MigrateNotificationDb(this IHost app)
    {
        using var serviceScope = app.Services.CreateScope();
        serviceScope.ServiceProvider.GetRequiredService<NotificationDbContext>().Database.Migrate();
    }

    public static IServiceCollection AddHostedServicesNotificationModule(this IServiceCollection services)
    {
        // New Channel-based email workers
        services.AddHostedService<EmailSendingWorker>();
        services.AddHostedService<EmailDbSweepWorker>();

        // Legacy SMS worker (unchanged)
        services.AddHostedService<SendSmsWorker>();

        return services;
    }
}
