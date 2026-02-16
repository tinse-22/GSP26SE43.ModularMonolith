using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Infrastructure.Logging;
using ClassifiedAds.Infrastructure.Monitoring;
using ClassifiedAds.Infrastructure.Web.ExceptionHandlers;
using ClassifiedAds.Infrastructure.Web.Validation;
using ClassifiedAds.Modules.Identity.Persistence;
using ClassifiedAds.Modules.Identity.Services;
using ClassifiedAds.Modules.Notification.Hubs;
using ClassifiedAds.Application.FeatureToggles;
using ClassifiedAds.Infrastructure.FeatureToggles.OutboxPublishingToggle;
using ClassifiedAds.WebAPI.ConfigurationOptions;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Microsoft.OpenApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// ═══════════════════════════════════════════════════════════════════════════════════
// ClassifiedAds.WebAPI - REST API Host
// ═══════════════════════════════════════════════════════════════════════════════════
// Responsibilities:
// - Expose REST API endpoints for all modules (thin controllers + CQRS dispatcher)
// - Handle authentication/authorization (JWT Bearer, IdentityServer/Auth0/Azure AD B2C)
// - Serve Swagger/Scalar API documentation
// - SignalR hub for real-time notifications
// - CORS, rate limiting, global exception handling
// ═══════════════════════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════════════════════
// Load environment variables from .env file (private data, not committed to git)
// Priority: .env.local > .env (allows per-developer overrides)
// Docker uses its own .env.docker via docker-compose env_file
// ═══════════════════════════════════════════════════════════════════════════════════
// Load .env file for private configuration (not committed to git)
// Probes up parent directories to find .env at solution root
dotenv.net.DotEnv.Fluent()
    .WithTrimValues()
    .WithProbeForEnv(probeLevelsToSearch: 6)
    .Load();

var builder = WebApplication.CreateBuilder(args);

// Add Aspire ServiceDefaults (OpenTelemetry, health checks, service discovery)
// This is optional and only activates when running under Aspire
builder.AddServiceDefaults();

// Add services to the container.
var services = builder.Services;
var configuration = builder.Configuration;

// Configure logging using custom Serilog setup
builder.WebHost.UseClassifiedAdsLogger(configuration =>
{
    var appSettings = new AppSettings();
    configuration.Bind(appSettings);
    return appSettings.Logging;
});

// Bind and validate AppSettings (fail-fast on misconfiguration)
var appSettings = new AppSettings();
configuration.Bind(appSettings);

// Note: WebAPI doesn't have Validate() method on AppSettings like Background does
// Consider adding validation in future if AppSettings grows complex enough
// For now, rely on runtime validation via IOptions<T> with ValidateDataAnnotations

services.Configure<AppSettings>(configuration);

// ═══════════════════════════════════════════════════════════════════════════════════
// Global Exception Handling
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddExceptionHandler<GlobalExceptionHandler>();
services.Configure<GlobalExceptionHandlerOptions>(opt =>
{
    opt.DetailLevel = builder.Environment.IsDevelopment()
        ? GlobalExceptionDetailLevel.ToString
        : GlobalExceptionDetailLevel.None;
});

// ═══════════════════════════════════════════════════════════════════════════════════
// Caching Configuration
// ═══════════════════════════════════════════════════════════════════════════════════
// Supports: InMemory, Redis, SQL Server (based on appsettings.Caching.Distributed.Provider)
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddCaches(appSettings.Caching);

// ═══════════════════════════════════════════════════════════════════════════════════
// FluentValidation Setup
// ═══════════════════════════════════════════════════════════════════════════════════
// Auto-discovers validators from all ClassifiedAds* assemblies (modules)
// Integrates with ASP.NET Core model validation pipeline
// ═══════════════════════════════════════════════════════════════════════════════════

// Register FluentValidation validators from all loaded assemblies
// This enables automatic discovery of validators when modules add them
services.AddValidatorsFromAssemblies(
    AppDomain.CurrentDomain.GetAssemblies().Where(a => a.FullName?.StartsWith("ClassifiedAds") == true),
    ServiceLifetime.Scoped,
    includeInternalTypes: true);

// Add FluentValidation to MVC pipeline for automatic model validation
services.AddFluentValidationAutoValidation(config =>
{
    // Disable DataAnnotations validation to avoid double validation
    // FluentValidation will handle all validation
    config.DisableDataAnnotationsValidation = false; // Keep DataAnnotations for backward compatibility
});

// ═══════════════════════════════════════════════════════════════════════════════════
// MVC Controllers & Module Registration
// ═══════════════════════════════════════════════════════════════════════════════════
// Registers controllers from all modules via .Add{Module}Module() extensions
// Each module contributes its controllers via ApplicationParts
// ═══════════════════════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════════════════════
// MVC Controllers & Module Registration
// ═══════════════════════════════════════════════════════════════════════════════════
// Registers controllers from all modules via .Add{Module}Module() extensions
// Each module contributes its controllers via ApplicationParts
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddControllers(configure =>
{
})
.ConfigureApiBehaviorOptions(options =>
{
    // Standardize validation error responses
    // Returns ValidationProblemDetails with traceId and consistent error format
    options.InvalidModelStateResponseFactory = ValidationProblemDetailsFactory.CreateFactory();
})
.AddJsonOptions(options =>
{
})
.AddAuditLogModule()
.AddConfigurationModule()
.AddIdentityModule()
.AddNotificationModule()
.AddStorageModule()
.AddSubscriptionModule()
.AddApiDocumentationModule();

// ═══════════════════════════════════════════════════════════════════════════════════
// SignalR (Real-time Notifications)
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddSignalR();

// ═══════════════════════════════════════════════════════════════════════════════════
// CORS Configuration
// ═══════════════════════════════════════════════════════════════════════════════════
// Configures multiple policies for API endpoints and SignalR hubs
// Uses appsettings.CORS.AllowedOrigins or AllowAnyOrigin flag
// ═══════════════════════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════════════════════
// CORS Configuration
// ═══════════════════════════════════════════════════════════════════════════════════
// Configures multiple policies for API endpoints and SignalR hubs
// Uses appsettings.CORS.AllowedOrigins or AllowAnyOrigin flag
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", builder => builder
        .WithOrigins(appSettings.CORS.AllowedOrigins)
        .AllowAnyMethod()
        .AllowAnyHeader());

    options.AddPolicy("SignalRHubs", builder => builder
        .WithOrigins(appSettings.CORS.AllowedOrigins)
        .AllowAnyHeader()
        .WithMethods("GET", "POST")
        .AllowCredentials());

    options.AddPolicy("AllowAnyOrigin", builder => builder
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());

    options.AddPolicy("CustomPolicy", builder => builder
        .AllowAnyOrigin()
        .WithMethods("Get")
        .WithHeaders("Content-Type"));
});

// ═══════════════════════════════════════════════════════════════════════════════════
// Date/Time Abstraction
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddDateTimeProvider();

// ═══════════════════════════════════════════════════════════════════════════════════
// Module Service Registration
// ═══════════════════════════════════════════════════════════════════════════════════
// Pattern: Chain .Add{Module}Module() calls, bind config from appsettings, set shared connection string
// Each module registers its own DbContext, repositories, commands, queries, event handlers
// Order matters: modules should be registered before .AddApplicationServices()
// ═══════════════════════════════════════════════════════════════════════════════════

var sharedConnectionString = configuration.GetConnectionString("Default");

services.AddAuditLogModule(opt =>
{
    configuration.GetSection("Modules:AuditLog").Bind(opt);
    opt.ConnectionStrings ??= new ClassifiedAds.Modules.AuditLog.ConfigurationOptions.ConnectionStringsOptions();
    opt.ConnectionStrings.Default = sharedConnectionString;
})
.AddConfigurationModule(opt =>
{
    configuration.GetSection("Modules:Configuration").Bind(opt);
    opt.ConnectionStrings ??= new ClassifiedAds.Modules.Configuration.ConfigurationOptions.ConnectionStringsOptions();
    opt.ConnectionStrings.Default = sharedConnectionString;
})
.AddIdentityModule(opt =>
{
    configuration.GetSection("Modules:Identity").Bind(opt);
    opt.ConnectionStrings ??= new ClassifiedAds.Modules.Identity.ConfigurationOptions.ConnectionStringsOptions();
    opt.ConnectionStrings.Default = sharedConnectionString;
})
.AddHttpCurrentUser()
.AddNotificationModule(opt =>
{
    configuration.GetSection("Modules:Notification").Bind(opt);
    opt.ConnectionStrings ??= new ClassifiedAds.Modules.Notification.ConfigurationOptions.ConnectionStringsOptions();
    opt.ConnectionStrings.Default = sharedConnectionString;
})
.AddStorageModule(opt =>
{
    configuration.GetSection("Modules:Storage").Bind(opt);
    opt.ConnectionStrings ??= new ClassifiedAds.Modules.Storage.ConfigurationOptions.ConnectionStringsOptions();
    opt.ConnectionStrings.Default = sharedConnectionString;
})
.AddSubscriptionModule(opt =>
{
    configuration.GetSection("Modules:Subscription").Bind(opt);
    opt.ConnectionStrings ??= new ClassifiedAds.Modules.Subscription.ConfigurationOptions.ConnectionStringsOptions();
    opt.ConnectionStrings.Default = sharedConnectionString;
    opt.PayOS ??= new ClassifiedAds.Modules.Subscription.ConfigurationOptions.PayOsOptions();
    configuration.GetSection("PayOS").Bind(opt.PayOS);
})
.AddApiDocumentationModule(opt =>
{
    configuration.GetSection("Modules:ApiDocumentation").Bind(opt);
    opt.ConnectionStrings ??= new ClassifiedAds.Modules.ApiDocumentation.ConfigurationOptions.ConnectionStringsOptions();
    opt.ConnectionStrings.Default = sharedConnectionString;
})
.AddApplicationServices();

// ═══════════════════════════════════════════════════════════════════════════════════
// HTML & PDF Utilities
// ═══════════════════════════════════════════════════════════════════════════════════
// Used by modules for generating reports, invoices, etc.
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddHtmlRazorLightEngine();
services.AddDinkToPdfConverter();

// ═══════════════════════════════════════════════════════════════════════════════════
// Background Workers (Notification Email/SMS sending)
// ═══════════════════════════════════════════════════════════════════════════════════
// Registers hosted services that consume from the email channel and send via SMTP
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddSingleton<IOutboxPublishingToggle, FileBasedOutboxPublishingToggle>();

// Register IMessageBus for outbox PublishEventWorkers that send messages via the bus
services.AddTransient<IMessageBus, MessageBus>();

services.AddHostedServicesNotificationModule();
services.AddHostedServicesApiDocumentationModule();

// ═══════════════════════════════════════════════════════════════════════════════════
// ASP.NET Core Data Protection
// ═══════════════════════════════════════════════════════════════════════════════════
// Keys persisted to database (IdentityModule) for multi-instance deployments
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddDataProtection()
    .PersistKeysToDbContext<IdentityDbContext>()
    .SetApplicationName("ClassifiedAds");

// ═══════════════════════════════════════════════════════════════════════════════════
// Authentication & Authorization
// ═══════════════════════════════════════════════════════════════════════════════════
// Supports: JWT Bearer tokens (self-issued or IdentityServer/Auth0/Azure AD B2C)
// Provider configured via appsettings.Authentication.Provider
// ═══════════════════════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════════════════════
// Authentication & Authorization
// ═══════════════════════════════════════════════════════════════════════════════════
// Supports: JWT Bearer tokens (self-issued or IdentityServer/Auth0/Azure AD B2C)
// Provider configured via appsettings.Authentication.Provider
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddAuthentication(options =>
{
    options.DefaultScheme = appSettings.Authentication.Provider switch
    {
        "Jwt" => "Jwt",
        _ => JwtBearerDefaults.AuthenticationScheme
    };
    options.DefaultChallengeScheme = options.DefaultScheme;
    options.DefaultAuthenticateScheme = options.DefaultScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = appSettings.Authentication.IdentityServer.Authority;
    options.Audience = appSettings.Authentication.IdentityServer.Audience;
    options.RequireHttpsMetadata = appSettings.Authentication.IdentityServer.RequireHttpsMetadata;
})
.AddJwtBearer("Jwt", options =>
{
    // Must match JwtTokenService signing configuration (Modules:Identity:Jwt)
    var jwtSecretKey = appSettings.Modules?.Identity?.Jwt?.SecretKey;
    if (string.IsNullOrWhiteSpace(jwtSecretKey) || jwtSecretKey.Length < 32)
    {
        throw new InvalidOperationException(
            "JWT Secret Key is invalid. Configure Modules:Identity:Jwt:SecretKey with at least 32 characters.");
    }

    var jwtIssuer = appSettings.Modules?.Identity?.Jwt?.Issuer;
    if (string.IsNullOrWhiteSpace(jwtIssuer))
    {
        jwtIssuer = appSettings.Authentication.Jwt.IssuerUri;
    }

    var jwtAudience = appSettings.Modules?.Identity?.Jwt?.Audience;
    if (string.IsNullOrWhiteSpace(jwtAudience))
    {
        jwtAudience = appSettings.Authentication.Jwt.Audience;
    }

    var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtSecretKey));

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = key,
        ValidateIssuerSigningKey = true,
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
    };

    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"JWT Auth Failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            // Check if the token has been blacklisted (e.g., after logout or password change)
            var blacklistService = context.HttpContext.RequestServices.GetService<ITokenBlacklistService>();
            if (blacklistService != null)
            {
                var jti = context.Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrEmpty(jti) && blacklistService.IsTokenBlacklisted(jti))
                {
                    context.Fail("Token has been revoked.");
                    return Task.CompletedTask;
                }
            }

            Console.WriteLine($"JWT Token Validated for user: {context.Principal?.Identity?.Name}");
            return Task.CompletedTask;
        }
    };
});

// ═══════════════════════════════════════════════════════════════════════════════════
// Swagger/OpenAPI Documentation
// ═══════════════════════════════════════════════════════════════════════════════════
// Generates API documentation with OAuth2/JWT authentication support
// UI available at /swagger and /scalar (modern alternative to Swagger UI)
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddSwaggerGen(setupAction =>
{
    setupAction.SwaggerDoc(
        $"ClassifiedAds",
        new OpenApiInfo()
        {
            Title = "ClassifiedAds API",
            Version = "1",
            Description = "ClassifiedAds API Specification.",
        });

    setupAction.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        Description = "Input your Bearer token to access this API",
    });

    setupAction.AddSecurityDefinition("Oidc", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            AuthorizationCode = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri(appSettings.Authentication.IdentityServer.Authority + "/connect/token", UriKind.Absolute),
                AuthorizationUrl = new Uri(appSettings.Authentication.IdentityServer.Authority + "/connect/authorize", UriKind.Absolute),
                Scopes = new Dictionary<string, string>
                {
                            { "openid", "OpenId" },
                            { "profile", "Profile" },
                            { "ClassifiedAds.WebAPI", "ClassifiedAds WebAPI" },
                },
            },
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri(appSettings.Authentication.IdentityServer.Authority + "/connect/token", UriKind.Absolute),
                Scopes = new Dictionary<string, string>
                {
                            { "ClassifiedAds.WebAPI", "ClassifiedAds WebAPI" },
                },
            },
        },
    });

    setupAction.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Oidc", doc), new List<string>()
        },
        {
            new OpenApiSecuritySchemeReference("Bearer", doc), new List<string>()
        },
    });
});

// ═══════════════════════════════════════════════════════════════════════════════════
// Monitoring & Observability
// ═══════════════════════════════════════════════════════════════════════════════════
// OpenTelemetry (traces, metrics) and Azure Application Insights support
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddMonitoringServices(appSettings.Monitoring);

// ═══════════════════════════════════════════════════════════════════════════════════
// HTTP Context & Current User
// ═══════════════════════════════════════════════════════════════════════════════════
// Provides access to authenticated user info throughout the application
// ═══════════════════════════════════════════════════════════════════════════════════

services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
services.AddScoped<ICurrentUser, CurrentWebUser>();

// ═══════════════════════════════════════════════════════════════════════════════════
// Configure the HTTP request pipeline.
// ═══════════════════════════════════════════════════════════════════════════════════
var app = builder.Build();

app.UseDebuggingMiddleware();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseExceptionHandler(options => { });

app.UseRouting();

app.UseCors(appSettings.CORS?.AllowAnyOrigin == true ? "AllowAnyOrigin" : "AllowedOrigins");

app.UseSwagger();

app.UseSwaggerUI(setupAction =>
{
    setupAction.SwaggerEndpoint("/swagger/ClassifiedAds/swagger.json", "ClassifiedAds API");
    setupAction.RoutePrefix = "swagger";
});

app.MapScalarApiReference(options =>
{
    options.WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json");
});

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notification").RequireCors("SignalRHubs");

// Map Aspire default endpoints (health checks)
app.MapDefaultEndpoints();

app.Run();
