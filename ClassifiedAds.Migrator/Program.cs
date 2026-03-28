using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Infrastructure.HealthChecks;
using ClassifiedAds.Infrastructure.Logging;
using ClassifiedAds.Modules.Identity.Persistence;
using ClassifiedAds.Modules.Identity.Services;
using DbUp;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Polly;
using System;
using System.Linq;
using System.Reflection;

// ═══════════════════════════════════════════════════════════════════════════════════
// ClassifiedAds.Migrator - Database Migration Runner
// ═══════════════════════════════════════════════════════════════════════════════════
// Responsibilities:
// - Apply EF Core migrations for all module DbContexts
// - Run DbUp scripts for supplemental SQL migrations
// - Ensure database is ready before WebAPI/Background start
// - Fail-fast if migration errors occur
//
// Pattern: Uses Host.CreateDefaultBuilder + UseClassifiedAdsLogger for logging setup
// Runs migrations then exits (not a long-running service)
// ═══════════════════════════════════════════════════════════════════════════════════

// Load .env for local parity with WebAPI/Background.
// Prevents migrator from targeting a different DB than runtime hosts.
var isRunningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

if (!isRunningInContainer)
{
    dotenv.net.DotEnv.Load(options: new dotenv.net.DotEnvOptions(
        probeForEnv: true,
        probeLevelsToSearch: 6,
        trimValues: true,
        overwriteExistingVars: false));
}

var verifyMigrationsOnly = args.Any(x => string.Equals(x, "--verify-migrations", StringComparison.OrdinalIgnoreCase));

var hostBuilder = Host.CreateDefaultBuilder(args)
.UseClassifiedAdsLogger(configuration =>
{
    return new LoggingOptions(); // Use default logging for migrator (minimal output)
})
.ConfigureServices((hostContext, services) =>
{
    var configuration = hostContext.Configuration;

    StartupDiagnostics.LogDatabaseTarget("Migrator", configuration, isRunningInContainer);

    // Optional: Wait for database to be available before attempting migrations
    // Enabled via CheckDependency:Enabled in appsettings (disabled under Aspire)
    if (bool.TryParse(configuration["CheckDependency:Enabled"], out var enabled) && enabled)
    {
        NetworkPortCheck.Wait(configuration["CheckDependency:Host"], 5);
    }

    // Register date/time abstraction
    services.AddDateTimeProvider();

    // Register caching (minimal for migrator, just in case modules need it)
    services.AddCaches(new ClassifiedAds.Infrastructure.Caching.CachingOptions
    {
        Distributed = new ClassifiedAds.Infrastructure.Caching.DistributedCacheOptions
        {
            Provider = "InMemory",
        },
    });

    // Get shared connection string for all modules
    var sharedConnectionString = configuration.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(sharedConnectionString))
    {
        throw new InvalidOperationException(
            "ConnectionStrings:Default is missing. Configure it via .env/.env.docker, user-secrets, or appsettings.");
    }

    // ═══════════════════════════════════════════════════════════════════════════════════
    // Module Registration for Migration
    // ═══════════════════════════════════════════════════════════════════════════════════
    // CRITICAL: Must set MigrationsAssembly to this project so EF Core finds migrations here
    // Pattern: Same as WebAPI/Background but with MigrationsAssembly override
    // Base Modules: AuditLog (cross-cutting), Identity, Storage
    // Core Modules: Configuration, Notification
    // Test Modules: TestGeneration, TestExecution, TestReporting
    // Business Modules: Subscription
    // ═══════════════════════════════════════════════════════════════════════════════════

    services.AddAuditLogModule(opt =>
    {
        configuration.GetSection("Modules:AuditLog").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.AuditLog.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })
    .AddIdentityModuleCore(opt =>
    {
        configuration.GetSection("Modules:Identity").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Identity.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })
    .AddStorageModule(opt =>
    {
        configuration.GetSection("Modules:Storage").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Storage.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })
    // Configuration Module
    .AddConfigurationModule(opt =>
    {
        configuration.GetSection("Modules:Configuration").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Configuration.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })
    // Notification Module
    .AddNotificationModule(opt =>
    {
        configuration.GetSection("Modules:Notification").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Notification.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })
    // Test Generation Module
    .AddTestGenerationModule(opt =>
    {
        configuration.GetSection("Modules:TestGeneration").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.TestGeneration.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })
    // Test Execution Module
    .AddTestExecutionModule(opt =>
    {
        configuration.GetSection("Modules:TestExecution").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.TestExecution.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })
    // Test Reporting Module
    .AddTestReportingModule(opt =>
    {
        configuration.GetSection("Modules:TestReporting").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.TestReporting.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })
    // Subscription Module
    .AddSubscriptionModule(opt =>
    {
        configuration.GetSection("Modules:Subscription").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.Subscription.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })
    // ApiDocumentation Module
    .AddApiDocumentationModule(opt =>
    {
        configuration.GetSection("Modules:ApiDocumentation").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.ApiDocumentation.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })

    // LlmAssistant Module
    .AddLlmAssistantModule(opt =>
    {
        configuration.GetSection("Modules:LlmAssistant").Bind(opt);
        opt.ConnectionStrings ??= new ClassifiedAds.Modules.LlmAssistant.ConfigurationOptions.ConnectionStringsOptions();
        opt.ConnectionStrings.Default = sharedConnectionString;
        opt.ConnectionStrings.MigrationsAssembly = Assembly.GetExecutingAssembly().GetName().Name;
    })
    .AddApplicationServices();

    // Add HTML and PDF utilities (some modules might reference these in migrations)
    services.AddHtmlRazorLightEngine();
    services.AddDinkToPdfConverter();

    // Configure ASP.NET Core Data Protection (keys persisted to database)
    services.AddDataProtection()
        .PersistKeysToDbContext<IdentityDbContext>()
        .SetApplicationName("ClassifiedAds");

    // Register HTTP context and current user (needed by some migration extension methods)
    services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
    services.AddScoped<ICurrentUser, CurrentWebUser>();
});

var app = hostBuilder.Build();
var configuration = app.Services.GetRequiredService<IConfiguration>();
var migrationVerifier = new ClassifiedAds.Migrator.MigrationModelSnapshotVerifier(app.Services);

Console.WriteLine("Verifying EF Core migration snapshots...");
migrationVerifier.VerifyNoPendingModelChanges();
Console.WriteLine("EF Core migration snapshots are up to date.");

if (verifyMigrationsOnly)
{
    return;
}

// ═══════════════════════════════════════════════════════════════════════════════════
// Run Migrations with Retry Policy
// ═══════════════════════════════════════════════════════════════════════════════════
// Uses Polly WaitAndRetry to handle transient database connection failures
// Runs all module migrations, then DbUp scripts
// ═══════════════════════════════════════════════════════════════════════════════════

Policy.Handle<Exception>().WaitAndRetry(
[
    TimeSpan.FromSeconds(10),
    TimeSpan.FromSeconds(20),
    TimeSpan.FromSeconds(30),
])
.Execute(() =>
{
    // Run EF Core migrations for each module DbContext
    // Base Modules: AuditLog, Identity, Storage
    app.MigrateAuditLogDb();
    app.MigrateIdentityDb();
    app.MigrateStorageDb();

    // Core Modules: Configuration, Notification
    app.MigrateConfigurationDb();
    app.MigrateNotificationDb();

    // Test Modules: TestGeneration, TestExecution, TestReporting
    app.MigrateTestGenerationDb();
    app.MigrateTestExecutionDb();
    app.MigrateTestReportingDb();

    // Business Modules: Subscription
    app.MigrateSubscriptionDb();

    // ApiDocumentation Module
    app.MigrateApiDocumentationDb();

    // LlmAssistant Module
    app.MigrateLlmAssistantDb();

    // Run DbUp scripts (for supplemental SQL migrations not in EF Core)
    var upgrader = DeployChanges.To
    .PostgresqlDatabase(configuration.GetConnectionString("Default"))
    .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
    .LogToConsole()
    .Build();

    var result = upgrader.PerformUpgrade();

    if (!result.Successful)
    {
        throw result.Error;
    }
});
