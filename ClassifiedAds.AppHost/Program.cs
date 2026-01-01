using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════════════
// Infrastructure Resources
// ═══════════════════════════════════════════════════════════════════════════════════

// PostgreSQL - Main database server
// Matches docker-compose: postgres:16, port 5432
var postgres = builder.AddPostgres("postgres")
    .WithImage("postgres")
    .WithImageTag("16")
    .WithEnvironment("POSTGRES_PASSWORD", "Postgres123@")
    .WithEnvironment("POSTGRES_USER", "postgres")
    .WithDataVolume("postgres_data")
    .WithPgAdmin();

// Add the main database (ClassifiedAds)
var classifiedAdsDb = postgres.AddDatabase("ClassifiedAds");

// RabbitMQ - Message broker with management UI
// Matches docker-compose: rabbitmq:3-management, ports 5672, 15672
var rabbitmq = builder.AddRabbitMQ("rabbitmq")
    .WithImage("rabbitmq")
    .WithImageTag("3-management")
    .WithManagementPlugin();

// Redis - Distributed cache
// Matches docker-compose: redis:7-alpine, port 6379
var redis = builder.AddRedis("redis")
    .WithImage("redis")
    .WithImageTag("7-alpine")
    .WithDataVolume("redis_data");

// MailHog - Email testing (SMTP + Web UI)
// Matches docker-compose: mailhog/mailhog, ports 1025 (SMTP), 8025 (Web UI)
var mailhog = builder.AddContainer("mailhog", "mailhog/mailhog")
    .WithHttpEndpoint(port: 8025, targetPort: 8025, name: "webui")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp", scheme: "tcp");

// ═══════════════════════════════════════════════════════════════════════════════════
// Database Migrator
// ═══════════════════════════════════════════════════════════════════════════════════

// Migrator - Runs EF Core migrations for all module DbContexts
// Must complete before WebAPI and Background services start
var migrator = builder.AddProject("migrator", "../ClassifiedAds.Migrator/ClassifiedAds.Migrator.csproj")
    .WithReference(classifiedAdsDb)
    .WithEnvironment("CheckDependency__Enabled", "false") // Aspire handles dependencies
    .WaitFor(postgres);

// ═══════════════════════════════════════════════════════════════════════════════════
// Application Services
// ═══════════════════════════════════════════════════════════════════════════════════

// WebAPI - REST API host with Swagger UI
// Depends on: PostgreSQL, RabbitMQ, Redis
// Wait for migrator to complete before starting
var webapi = builder.AddProject("webapi", "../ClassifiedAds.WebAPI/ClassifiedAds.WebAPI.csproj")
    .WithReference(classifiedAdsDb)
    .WithReference(rabbitmq)
    .WithReference(redis)
    // Configure Redis for distributed caching
    .WithEnvironment("Caching__Distributed__Provider", "Redis")
    .WithEnvironment("Caching__Distributed__Redis__InstanceName", "ClassifiedAds_")
    // Configure RabbitMQ messaging
    .WithEnvironment("Messaging__Provider", "RabbitMQ")
    .WaitFor(migrator)
    .WaitFor(rabbitmq)
    .WaitFor(redis)
    .WithExternalHttpEndpoints();

// Background - Background worker service
// Depends on: PostgreSQL, RabbitMQ, MailHog, Redis
// Wait for migrator to complete before starting
var background = builder.AddProject("background", "../ClassifiedAds.Background/ClassifiedAds.Background.csproj")
    .WithReference(classifiedAdsDb)
    .WithReference(rabbitmq)
    .WithReference(redis)
    // Configure Redis for distributed caching
    .WithEnvironment("Caching__Distributed__Provider", "Redis")
    .WithEnvironment("Caching__Distributed__Redis__InstanceName", "ClassifiedAds_")
    // Configure RabbitMQ messaging
    .WithEnvironment("Messaging__Provider", "RabbitMQ")
    // Configure email via MailHog
    .WithEnvironment("Modules__Notification__Email__Provider", "SmtpClient")
    .WithEnvironment("Modules__Notification__Email__SmtpClient__Host", "{mailhog.bindings.smtp.host}")
    .WithEnvironment("Modules__Notification__Email__SmtpClient__Port", "{mailhog.bindings.smtp.port}")
    // Configure SMS provider (fake for development)
    .WithEnvironment("Modules__Notification__Sms__Provider", "Fake")
    // Configure web notifications provider (fake for development)
    .WithEnvironment("Modules__Notification__Web__Provider", "Fake")
    // Configure storage provider
    .WithEnvironment("Modules__Storage__Provider", "Local")
    .WithEnvironment("Modules__Storage__Local__Path", "/tmp/files")
    .WaitFor(migrator)
    .WaitFor(rabbitmq)
    .WaitFor(redis);

// Build and run the Aspire application
builder.Build().Run();
