using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Infrastructure.Logging;
using ClassifiedAds.Infrastructure.Monitoring;
using ClassifiedAds.Infrastructure.Web.ExceptionHandlers;
using ClassifiedAds.Infrastructure.Web.Validation;
using ClassifiedAds.Modules.Identity.Persistence;
using ClassifiedAds.Modules.Identity.Services;
using ClassifiedAds.Modules.Notification.Hubs;
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

var builder = WebApplication.CreateBuilder(args);

// Add Aspire ServiceDefaults (OpenTelemetry, health checks, service discovery)
// This is optional and only activates when running under Aspire
builder.AddServiceDefaults();

// Add services to the container.
var services = builder.Services;
var configuration = builder.Configuration;

builder.WebHost.UseClassifiedAdsLogger(configuration =>
{
    var appSettings = new AppSettings();
    configuration.Bind(appSettings);
    return appSettings.Logging;
});

var appSettings = new AppSettings();
configuration.Bind(appSettings);

services.Configure<AppSettings>(configuration);

services.AddExceptionHandler<GlobalExceptionHandler>();

services.AddCaches(appSettings.Caching);

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
.AddProductModule()
.AddStorageModule();

services.AddSignalR();

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

services.AddDateTimeProvider();

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

services.AddHtmlRazorLightEngine();
services.AddDinkToPdfConverter();

services.AddDataProtection()
    .PersistKeysToDbContext<IdentityDbContext>()
    .SetApplicationName("ClassifiedAds");

services.AddAuthentication(options =>
{
    options.DefaultScheme = appSettings.Authentication.Provider switch
    {
        "Jwt" => "Jwt",
        _ => JwtBearerDefaults.AuthenticationScheme
    };
})
.AddJwtBearer(options =>
{
    options.Authority = appSettings.Authentication.IdentityServer.Authority;
    options.Audience = appSettings.Authentication.IdentityServer.Audience;
    options.RequireHttpsMetadata = appSettings.Authentication.IdentityServer.RequireHttpsMetadata;
})
.AddJwtBearer("Jwt", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = appSettings.Authentication.Jwt.IssuerUri,
        ValidAudience = appSettings.Authentication.Jwt.Audience,
        TokenDecryptionKey = new X509SecurityKey(appSettings.Authentication.Jwt.TokenDecryptionCertificate.FindCertificate()),
        IssuerSigningKey = new X509SecurityKey(appSettings.Authentication.Jwt.IssuerSigningCertificate.FindCertificate()),
    };
});

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

services.AddMonitoringServices(appSettings.Monitoring);

services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
services.AddScoped<ICurrentUser, CurrentWebUser>();

// Configure the HTTP request pipeline.
var app = builder.Build();

app.UseDebuggingMiddleware();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseExceptionHandler(options => { });

app.UseRouting();

app.UseCors(appSettings.CORS.AllowAnyOrigin ? "AllowAnyOrigin" : "AllowedOrigins");

app.UseSwagger();

app.MapScalarApiReference();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notification").RequireCors("SignalRHubs");

// Map Aspire default endpoints (health checks)
app.MapDefaultEndpoints();

app.Run();
