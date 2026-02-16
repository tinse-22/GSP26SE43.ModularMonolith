using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;

namespace ClassifiedAds.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory for integration testing.
/// Configures the application to use Testcontainers PostgreSQL and test authentication.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly Action<IServiceCollection>? _configureAdditionalTestServices;

    public CustomWebApplicationFactory(
        string connectionString,
        Action<IServiceCollection>? configureAdditionalTestServices = null)
    {
        _connectionString = connectionString;
        _configureAdditionalTestServices = configureAdditionalTestServices;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration for testing
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                // Database connection string (Testcontainers PostgreSQL)
                ["ConnectionStrings:Default"] = _connectionString,

                // Disable external services
                ["Caching:Distributed:Provider"] = "InMemory",
                ["Monitoring:AzureApplicationInsights:IsEnabled"] = "false",
                ["Monitoring:OpenTelemetry:IsEnabled"] = "false",
                ["Logging:OpenTelemetry:IsEnabled"] = "false",

                // Configure authentication to use test scheme
                ["Authentication:Provider"] = "Test",

                // CORS - allow all for tests
                ["CORS:AllowAnyOrigin"] = "true",
            });
        });

        builder.ConfigureTestServices(services =>
        {
            // Replace authentication with test scheme
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthHandler.AuthenticationScheme;
                options.DefaultScheme = TestAuthHandler.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.AuthenticationScheme,
                options => { });

            _configureAdditionalTestServices?.Invoke(services);

            // Ensure database is created and migrations applied
            var sp = services.BuildServiceProvider();

            // Run migrations or ensure database is created
            // The modules use separate DbContexts, so we need to ensure all are migrated
            EnsureDatabaseCreated(sp);
        });
    }

    private static void EnsureDatabaseCreated(ServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var scopedServices = scope.ServiceProvider;

        // Get all DbContext types and ensure they're created
        // We iterate through known module DbContext types
        var dbContextTypes = new[]
        {
            typeof(ClassifiedAds.Modules.Identity.Persistence.IdentityDbContext),
            typeof(ClassifiedAds.Modules.AuditLog.Persistence.AuditLogDbContext),
            typeof(ClassifiedAds.Modules.Configuration.Persistence.ConfigurationDbContext),
            typeof(ClassifiedAds.Modules.Notification.Persistence.NotificationDbContext),
            typeof(ClassifiedAds.Modules.Storage.Persistence.StorageDbContext),
            typeof(ClassifiedAds.Modules.Subscription.Persistence.SubscriptionDbContext),
        };

        foreach (var dbContextType in dbContextTypes)
        {
            try
            {
                if (scopedServices.GetService(dbContextType) is DbContext dbContext)
                {
                    dbContext.Database.EnsureCreated();
                }
            }
            catch (Exception)
            {
                // Some DbContexts may not be available in test configuration
                // Continue with others
            }
        }
    }
}
