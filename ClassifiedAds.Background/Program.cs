using ClassifiedAds.Application.FeatureToggles;
using ClassifiedAds.Background.ConfigurationOptions;
using ClassifiedAds.Background.Identity;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Infrastructure.FeatureToggles.OutboxPublishingToggle;
using ClassifiedAds.Infrastructure.Logging;
using ClassifiedAds.Infrastructure.Monitoring;
using ClassifiedAds.Modules.Identity.Persistence;
using ClassifiedAds.Modules.Storage.DTOs;
using ClassifiedAds.Modules.Storage.MessageBusConsumers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

// ═══════════════════════════════════════════════════════════════════════════════════
// ClassifiedAds.Background - Background Worker Host
// ═══════════════════════════════════════════════════════════════════════════════════
// Responsibilities:
// - Publish outbox messages to message bus (cross-module async communication)
// - Send email/SMS/web notifications
// - Consume message bus events from other modules
// - Run scheduled background tasks
//
// Pattern: Uses Host.CreateDefaultBuilder + UseClassifiedAdsLogger for logging setup
// Aspire ServiceDefaults added via ConfigureServices (activates only under Aspire)
// ═══════════════════════════════════════════════════════════════════════════════════

Host.CreateDefaultBuilder(args)
.UseWindowsService()
.UseClassifiedAdsLogger(configuration =>
{
    var appSettings = new AppSettings();
    configuration.Bind(appSettings);
    return appSettings.Logging;
})
.ConfigureServices((hostContext, services) =>
{
    var configuration = hostContext.Configuration;

    // Bind and validate AppSettings (fail-fast on misconfiguration)
    var appSettings = new AppSettings();
    configuration.Bind(appSettings);

    var validationResult = appSettings.Validate();
    if (validationResult.Failed)
    {
        throw new Exception(validationResult.FailureMessage);
    }

    services.Configure<AppSettings>(configuration);

    // Add monitoring services (OpenTelemetry, Application Insights)
    services.AddMonitoringServices(appSettings.Monitoring);

    // Register current user implementation for background context
    services.AddScoped<ICurrentUser, CurrentUser>();

    // Register date/time abstraction
    services.AddDateTimeProvider();

    // Configure caching (InMemory, Redis, SQL Server)
    services.AddCaches(appSettings.Caching);

    // Get shared connection string for all modules
    var sharedConnectionString = configuration.GetConnectionString("Default");

    // ═══════════════════════════════════════════════════════════════════════════════════
    // Module Registration
    // ═══════════════════════════════════════════════════════════════════════════════════
    // Pattern: Chain .Add{Module}Module() calls, bind config from appsettings, set shared connection string
    // Each module registers its own DbContext, repositories, commands, queries, event handlers
    // Note: ConfigurationModule not needed in Background (only used in WebAPI for runtime config)
    // ═══════════════════════════════════════════════════════════════════════════════════

    services
    .AddAuditLogModule(opt =>
    {
        configuration.GetSection("Modules:AuditLog").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.AuditLog.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
    })
    .AddIdentityModuleCore(opt =>
    {
        configuration.GetSection("Modules:Identity").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Identity.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
    })
    .AddNotificationModule(opt =>
    {
        configuration.GetSection("Modules:Notification").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Notification.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
    })
    .AddProductModule(opt =>
    {
        configuration.GetSection("Modules:Product").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Product.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
    })
    .AddStorageModule(opt =>
    {
        configuration.GetSection("Modules:Storage").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Storage.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
    })
    .AddApplicationServices();

    // Add HTML and PDF utilities (used by notification module)
    services.AddHtmlRazorLightEngine();
    services.AddDinkToPdfConverter();

    // Configure ASP.NET Core Data Protection (keys persisted to database)
    services.AddDataProtection()
    .PersistKeysToDbContext<IdentityDbContext>()
    .SetApplicationName("ClassifiedAds");

    // ═══════════════════════════════════════════════════════════════════════════════════
    // Message Bus Configuration
    // ═══════════════════════════════════════════════════════════════════════════════════
    // Registers message bus sender/receiver for async cross-module communication
    // Uses RabbitMQ/Kafka/Azure Service Bus based on appsettings.Messaging.Provider
    // ═══════════════════════════════════════════════════════════════════════════════════

    services.AddTransient<IMessageBus, MessageBus>();
    services.AddMessageBusSender<FileUploadedEvent>(appSettings.Messaging);
    services.AddMessageBusSender<FileDeletedEvent>(appSettings.Messaging);
    services.AddMessageBusReceiver<WebhookConsumer, FileUploadedEvent>(appSettings.Messaging);
    services.AddMessageBusReceiver<WebhookConsumer, FileDeletedEvent>(appSettings.Messaging);

    // Register feature toggles (e.g., file-based outbox publishing toggle)
    AddFeatureToggles(services);

    // Register hosted services (background workers) from all modules
    AddHostedServices(services);
})
.Build()
.Run();

static void AddFeatureToggles(IServiceCollection services)
{
    services.AddSingleton<IOutboxPublishingToggle, FileBasedOutboxPublishingToggle>();
}

static void AddHostedServices(IServiceCollection services)
{
    services.AddHostedServicesIdentityModule();
    services.AddHostedServicesNotificationModule();
    services.AddHostedServicesProductModule();
    services.AddHostedServicesStorageModule();
}
