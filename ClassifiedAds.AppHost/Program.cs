using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.Configuration;
using System;
using System.Data.Common;

// ═══════════════════════════════════════════════════════════════════════════════════
// ClassifiedAds.AppHost - .NET Aspire Orchestration
// ═══════════════════════════════════════════════════════════════════════════════════
// Responsibilities:
// - Define infrastructure resources (RabbitMQ, Redis, MailHog)
// - Orchestrate application services (WebAPI, Background, Migrator)
// - Manage startup dependencies (Migrator runs before WebAPI/Background)
// - Provide Aspire Dashboard for observability (traces, metrics, logs, resources)
// - Inject environment variables and service discovery automatically
// 
// Run: dotnet run --project ClassifiedAds.AppHost
// Dashboard: https://localhost:17274 (or URL shown in console)
// ═══════════════════════════════════════════════════════════════════════════════════

var isRunningInContainer = string.Equals(
    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
    "true",
    StringComparison.OrdinalIgnoreCase);

var processConnectionStringBeforeDotEnv = Environment.GetEnvironmentVariable("ConnectionStrings__Default");

if (!isRunningInContainer)
{
    dotenv.net.DotEnv.Load(options: new dotenv.net.DotEnvOptions(
        probeForEnv: true,
        probeLevelsToSearch: 6,
        trimValues: true,
        overwriteExistingVars: false));
}

var explicitDatabaseMode = Environment.GetEnvironmentVariable("APPHOST_DATABASE_MODE");
var builder = DistributedApplication.CreateBuilder(args);
var configuredConnectionString = builder.Configuration.GetConnectionString("Default");
var useExternalDatabase = ResolveUseExternalDatabase(explicitDatabaseMode, processConnectionStringBeforeDotEnv);
var externalConnectionString = default(string);

if (useExternalDatabase)
{
    externalConnectionString = !string.IsNullOrWhiteSpace(processConnectionStringBeforeDotEnv)
        ? processConnectionStringBeforeDotEnv
        : configuredConnectionString;

    if (string.IsNullOrWhiteSpace(externalConnectionString))
    {
        throw new InvalidOperationException(
            "External DB mode requires ConnectionStrings__Default. " +
            "Set it in current shell or .env before running AppHost.");
    }

    var externalDatabaseHost = ResolveDatabaseHost(externalConnectionString);
    if (string.IsNullOrWhiteSpace(externalDatabaseHost))
    {
        throw new InvalidOperationException("Could not resolve database host from ConnectionStrings__Default.");
    }

    Console.WriteLine($"[AppHost] Database mode: External ({externalDatabaseHost})");

    if (LooksLikeSupabaseTransactionPooler(externalConnectionString))
    {
        Console.WriteLine(
            "[AppHost] Warning: ConnectionStrings__Default points to Supabase transaction pooler (port 6543). " +
            "This path can cause intermittent Npgsql ObjectDisposedException for migrator/runtime. " +
            "Prefer local AppHost DB mode or Supabase session mode (5432).");
    }
}
else
{
    Console.WriteLine("[AppHost] Database mode: Local PostgreSQL container (db/ClassifiedAds, host port 55433)");
}

var externalRedisUrl = builder.Configuration["REDIS_URL"];
var redisInstanceName = builder.Configuration["Caching__Distributed__Redis__InstanceName"] ?? "ClassifiedAds_";
var useExternalRedis = !string.IsNullOrWhiteSpace(externalRedisUrl);
Console.WriteLine(
    useExternalRedis
        ? "[AppHost] Redis mode: External (REDIS_URL)"
        : "[AppHost] Redis mode: Local container");

// ═══════════════════════════════════════════════════════════════════════════════════
// Infrastructure Resources
// ═══════════════════════════════════════════════════════════════════════════════════

// RabbitMQ - Message broker with management UI
// Matches docker-compose: rabbitmq:3-management, ports 5672 (AMQP), 15672 (Management UI)
// Used for async cross-module communication via message bus
var rabbitmqPassword = builder.AddParameter("rabbitmq-password", secret: true);
var rabbitmq = builder.AddRabbitMQ("rabbitmq", password: rabbitmqPassword)
    .WithManagementPlugin();  // Aspire automatically uses rabbitmq:3-management image

// Redis - Distributed cache
// Matches docker-compose: redis:7-alpine, port 6379
// Used for distributed caching across multiple instances
var redis = !useExternalRedis
    ? builder.AddRedis("redis")
        .WithHostPort(6379)
        .WithDataVolume("redis_data")  // Aspire uses latest stable Redis image
    : null;

// MailHog - Email testing (SMTP + Web UI)
// Matches docker-compose: mailhog/mailhog, ports 1025 (SMTP), 8025 (Web UI)
// Captures all outgoing emails for testing (no actual emails sent)
var mailhog = builder.AddContainer("mailhog", "mailhog/mailhog")
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "webui")  // View captured emails
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp", scheme: "tcp");  // SMTP server

IResourceBuilder<PostgresDatabaseResource>? localDatabase = null;
if (!useExternalDatabase)
{
    var pgPassword = builder.AddParameter("postgres-password", secret: true);
    var postgres = builder.AddPostgres("db", password: pgPassword, port: 55433)
        .WithImageTag("16")
        .WithDataVolume("classifiedads_apphost_postgres_data");

    localDatabase = postgres.AddDatabase("classifiedads", "ClassifiedAds");
}

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
        .WaitFor(localDatabase!)
        .WithEnvironment("ConnectionStrings__Default", localDatabase!.Resource.ConnectionStringExpression);
}

// ═══════════════════════════════════════════════════════════════════════════════════
// Application Services
// ═══════════════════════════════════════════════════════════════════════════════════

// WebAPI - REST API host with Swagger UI
// Depends on: selected database mode, RabbitMQ, Redis
// Waits for migrator to complete before starting
var webapi = builder.AddProject("webapi", "../ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj")
    .WithEndpoint("http", endpoint => endpoint.Port = 9002)  // Keep the existing HTTP endpoint, pin its host port
    .WithReference(rabbitmq)         // Injects RabbitMQ connection details
                                     // Override appsettings for Aspire environment
    .WithEnvironment("Caching__Distributed__Provider", "Redis")
    .WithEnvironment("Caching__Distributed__Redis__InstanceName", redisInstanceName)
    .WithEnvironment("Messaging__Provider", "RabbitMQ")
    .WaitForCompletion(migrator)  // Ensures migrations complete before startup
    .WaitFor(rabbitmq)
    .WithExternalHttpEndpoints();  // Exposes to localhost for external access

if (useExternalRedis)
{
    webapi = webapi
        .WithEnvironment("REDIS_URL", externalRedisUrl!)
        .WithEnvironment("Caching__Distributed__Redis__Configuration", externalRedisUrl!);
}
else
{
    webapi = webapi
        .WithReference(redis!)           // Injects Redis connection details
        .WaitFor(redis!);
}

if (useExternalDatabase)
{
    webapi = webapi.WithEnvironment("ConnectionStrings__Default", externalConnectionString!);
}
else
{
    webapi = webapi
        .WaitFor(localDatabase!)
        .WithEnvironment("ConnectionStrings__Default", localDatabase!.Resource.ConnectionStringExpression);
}

// Background - Background worker service
// Depends on: selected database mode, RabbitMQ, MailHog, Redis
// Waits for migrator to complete before starting
// Responsibilities: Publish outbox messages, send emails/SMS, consume message bus events
var background = builder.AddProject("background", "../ClassifiedAds.Background/ClassifiedAds.Background.csproj")
    .WithHttpEndpoint(port: 9003, name: "http")  // Fixed host port
    .WithReference(rabbitmq)
    // Override appsettings for Aspire environment
    .WithEnvironment("Caching__Distributed__Provider", "Redis")
    .WithEnvironment("Caching__Distributed__Redis__InstanceName", redisInstanceName)
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
    .WaitForCompletion(migrator)
    .WaitFor(rabbitmq)
    .WaitFor(mailhog);

if (useExternalRedis)
{
    background = background
        .WithEnvironment("REDIS_URL", externalRedisUrl!)
        .WithEnvironment("Caching__Distributed__Redis__Configuration", externalRedisUrl!);
}
else
{
    background = background
        .WithReference(redis!)
        .WaitFor(redis!);
}

if (useExternalDatabase)
{
    background = background.WithEnvironment("ConnectionStrings__Default", externalConnectionString!);
}
else
{
    background = background
        .WaitFor(localDatabase!)
        .WithEnvironment("ConnectionStrings__Default", localDatabase!.Resource.ConnectionStringExpression);
}

// ═══════════════════════════════════════════════════════════════════════════════════
// Build and Run
// ═══════════════════════════════════════════════════════════════════════════════════
// Starts all resources and services, launches Aspire Dashboard
// Dashboard provides: logs, traces, metrics, resource status, environment variables
// ═══════════════════════════════════════════════════════════════════════════════════

builder.Build().Run();

bool ResolveUseExternalDatabase(string? databaseMode, string? processConnectionString)
{
    if (string.Equals(databaseMode, "external", StringComparison.OrdinalIgnoreCase))
    {
        return true;
    }

    if (string.Equals(databaseMode, "local", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    // Auto mode: only use external DB when the shell explicitly exported ConnectionStrings__Default.
    // Values coming from .env should not force AppHost out of local DB mode.
    return !string.IsNullOrWhiteSpace(processConnectionString);
}

string ResolveDatabaseHost(string connectionString)
{
    var csBuilder = new DbConnectionStringBuilder
    {
        ConnectionString = connectionString,
    };

    if (csBuilder.TryGetValue("Host", out var hostValue) && hostValue is not null)
    {
        return Convert.ToString(hostValue) ?? string.Empty;
    }

    if (csBuilder.TryGetValue("Server", out var serverValue) && serverValue is not null)
    {
        return Convert.ToString(serverValue) ?? string.Empty;
    }

    return string.Empty;
}

string ResolveDatabasePort(string connectionString)
{
    var csBuilder = new DbConnectionStringBuilder
    {
        ConnectionString = connectionString,
    };

    if (csBuilder.TryGetValue("Port", out var portValue) && portValue is not null)
    {
        return Convert.ToString(portValue) ?? string.Empty;
    }

    return string.Empty;
}

bool LooksLikeSupabaseTransactionPooler(string connectionString)
{
    var host = ResolveDatabaseHost(connectionString);
    var port = ResolveDatabasePort(connectionString);

    return host.IndexOf(".pooler.supabase.com", StringComparison.OrdinalIgnoreCase) >= 0 &&
        string.Equals(port, "6543", StringComparison.OrdinalIgnoreCase);
}
