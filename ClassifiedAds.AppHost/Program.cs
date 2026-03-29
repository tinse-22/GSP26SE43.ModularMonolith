using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;

// ═══════════════════════════════════════════════════════════════════════════════════
// ClassifiedAds.AppHost - .NET Aspire Orchestration
// ═══════════════════════════════════════════════════════════════════════════════════
// Responsibilities:
// - Define infrastructure resources (PostgreSQL, RabbitMQ, Redis, MailHog)
// - Orchestrate application services (WebAPI, Background, Migrator)
// - Manage startup dependencies (Migrator runs before WebAPI/Background)
// - Provide Aspire Dashboard for observability (traces, metrics, logs, resources)
// - Inject environment variables and service discovery automatically
// 
// Run: dotnet run --project ClassifiedAds.AppHost
// Dashboard: https://localhost:17274 (or URL shown in console)
// ═══════════════════════════════════════════════════════════════════════════════════

const string AppHostPostgresVolumeName = "classifiedads_apphost_postgres_data";
const string AppHostPostgresDatabaseName = "ClassifiedAds";
const int AppHostPostgresHostPort = 5432;

var builder = DistributedApplication.CreateBuilder(args);
var externalConnectionString = builder.Configuration.GetConnectionString("Default");
var useExternalDatabase = !string.IsNullOrWhiteSpace(externalConnectionString);
Console.WriteLine(
    useExternalDatabase
        ? "[AppHost] Database mode: External (ConnectionStrings__Default)"
        : $"[AppHost] Database mode: Local PostgreSQL container with persistent volume '{AppHostPostgresVolumeName}'");

// ═══════════════════════════════════════════════════════════════════════════════════
// Infrastructure Resources
// ═══════════════════════════════════════════════════════════════════════════════════

// In external mode, AppHost uses externally provided ConnectionStrings__Default (e.g. standalone docker-compose DB).
// In local mode, AppHost provisions a local PostgreSQL container and injects ConnectionStrings__Default automatically.
var postgres = default(IResourceBuilder<PostgresServerResource>);
var classifiedAdsDb = default(IResourceBuilder<PostgresDatabaseResource>);

if (!useExternalDatabase)
{
    var postgresPassword = builder.AddParameter(
        "postgres-password",
        new GenerateParameterDefault
        {
            MinLength = 24,
            Lower = true,
            Upper = true,
            Numeric = true,
            Special = false,
            MinLower = 1,
            MinUpper = 1,
            MinNumeric = 1,
        },
        secret: true,
        persist: true);

    postgres = builder.AddPostgres("postgres", password: postgresPassword)
        .WithImage("postgres")
        .WithImageTag("16")
        .WithHostPort(AppHostPostgresHostPort)
        .WithDataVolume(AppHostPostgresVolumeName)
        .WithPgAdmin();                   // Adds PgAdmin UI for database management

    // Add the main database
    // Using "Default" as database resource name so Aspire injects ConnectionStrings__Default
    // which matches the key used in appsettings.json
    classifiedAdsDb = postgres.AddDatabase("Default", databaseName: AppHostPostgresDatabaseName);

    Console.WriteLine($"[AppHost] Local PostgreSQL database: {AppHostPostgresDatabaseName}");
    Console.WriteLine($"[AppHost] Local PostgreSQL host port: {AppHostPostgresHostPort}");
    Console.WriteLine($"[AppHost] Local PostgreSQL volume: {AppHostPostgresVolumeName}");
}
else
{
    Console.WriteLine("[AppHost] Local PostgreSQL container is disabled because ConnectionStrings__Default was supplied.");
}

// RabbitMQ - Message broker with management UI
// Matches docker-compose: rabbitmq:3-management, ports 5672 (AMQP), 15672 (Management UI)
// Used for async cross-module communication via message bus
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithManagementPlugin();  // Aspire automatically uses rabbitmq:3-management image

// Redis - Distributed cache
// Matches docker-compose: redis:7-alpine, port 6379
// Used for distributed caching across multiple instances
var redis = builder.AddRedis("redis")
    .WithDataVolume("redis_data");  // Aspire uses latest stable Redis image

// MailHog - Email testing (SMTP + Web UI)
// Matches docker-compose: mailhog/mailhog, ports 1025 (SMTP), 8025 (Web UI)
// Captures all outgoing emails for testing (no actual emails sent)
var mailhog = builder.AddContainer("mailhog", "mailhog/mailhog")
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "webui")  // View captured emails
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp", scheme: "tcp");  // SMTP server

// ═══════════════════════════════════════════════════════════════════════════════════
// Database Migrator
// ═══════════════════════════════════════════════════════════════════════════════════

// Migrator - Runs EF Core migrations for all module DbContexts
// Must complete successfully before WebAPI and Background services start
// Ensures database schema is up-to-date before application processes requests
var migrator = builder.AddProject("migrator", "../ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj")
    .WithEnvironment("CheckDependency__Enabled", "false"); // Aspire handles dependencies, no need for manual port check

if (useExternalDatabase)
{
    migrator = migrator.WithEnvironment("ConnectionStrings__Default", externalConnectionString!);
}
else
{
    migrator = migrator
        .WithReference(classifiedAdsDb!) // Injects connection string automatically
        .WaitFor(postgres!);  // Waits for PostgreSQL to be ready before starting
}

// ═══════════════════════════════════════════════════════════════════════════════════
// Application Services
// ═══════════════════════════════════════════════════════════════════════════════════

// WebAPI - REST API host with Swagger UI
// Depends on: PostgreSQL, RabbitMQ, Redis
// Waits for migrator to complete before starting
var webapi = builder.AddProject("webapi", "../ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj")
    .WithReference(rabbitmq)         // Injects RabbitMQ connection details
    .WithReference(redis)            // Injects Redis connection details
                                     // Override appsettings for Aspire environment
    .WithEnvironment("Caching__Distributed__Provider", "Redis")
    .WithEnvironment("Caching__Distributed__Redis__InstanceName", "ClassifiedAds_")
    .WithEnvironment("Messaging__Provider", "RabbitMQ")
    .WaitFor(migrator)  // Ensures migrations complete first
    .WaitFor(rabbitmq)
    .WaitFor(redis)
    .WithExternalHttpEndpoints();  // Exposes to localhost for external access

if (useExternalDatabase)
{
    webapi = webapi.WithEnvironment("ConnectionStrings__Default", externalConnectionString!);
}
else
{
    webapi = webapi.WithReference(classifiedAdsDb!);  // Injects ConnectionStrings__Default
}

// Background - Background worker service
// Depends on: PostgreSQL, RabbitMQ, MailHog, Redis
// Waits for migrator to complete before starting
// Responsibilities: Publish outbox messages, send emails/SMS, consume message bus events
var background = builder.AddProject("background", "../ClassifiedAds.Background/ClassifiedAds.Background.csproj")
    .WithReference(rabbitmq)
    .WithReference(redis)
    // Override appsettings for Aspire environment
    .WithEnvironment("Caching__Distributed__Provider", "Redis")
    .WithEnvironment("Caching__Distributed__Redis__InstanceName", "ClassifiedAds_")
    .WithEnvironment("Messaging__Provider", "RabbitMQ")
    // Configure email via MailHog (for testing, no real emails sent)
    .WithEnvironment("Modules__Notification__Email__Provider", "SmtpClient")
    .WithEnvironment("Modules__Notification__Email__SmtpClient__Host", mailhog.GetEndpoint("smtp").Property(EndpointProperty.Host))
    .WithEnvironment("Modules__Notification__Email__SmtpClient__Port", mailhog.GetEndpoint("smtp").Property(EndpointProperty.Port))
    // Configure SMS and Web notifications (fake providers for development)
    .WithEnvironment("Modules__Notification__Sms__Provider", "Fake")
    .WithEnvironment("Modules__Notification__Web__Provider", "Fake")
    // Configure file storage (local filesystem for development)
    .WithEnvironment("Modules__Storage__Provider", "Local")
    .WithEnvironment("Modules__Storage__Local__Path", "/tmp/files")
    .WaitFor(migrator)
    .WaitFor(rabbitmq)
    .WaitFor(redis)
    .WaitFor(mailhog);

if (useExternalDatabase)
{
    background = background.WithEnvironment("ConnectionStrings__Default", externalConnectionString!);
}
else
{
    background = background.WithReference(classifiedAdsDb!);
}

// ═══════════════════════════════════════════════════════════════════════════════════
// Build and Run
// ═══════════════════════════════════════════════════════════════════════════════════
// Starts all resources and services, launches Aspire Dashboard
// Dashboard provides: logs, traces, metrics, resource status, environment variables
// ═══════════════════════════════════════════════════════════════════════════════════

builder.Build().Run();
