using ClassifiedAds.Modules.ApiDocumentation.Persistence;
using ClassifiedAds.Modules.AuditLog.Persistence;
using ClassifiedAds.Modules.Configuration.Persistence;
using ClassifiedAds.Modules.Identity.Persistence;
using ClassifiedAds.Modules.LlmAssistant.Persistence;
using ClassifiedAds.Modules.Notification.Persistence;
using ClassifiedAds.Modules.Storage.Persistence;
using ClassifiedAds.Modules.Subscription.Persistence;
using ClassifiedAds.Modules.TestExecution.Persistence;
using ClassifiedAds.Modules.TestGeneration.Persistence;
using ClassifiedAds.Modules.TestReporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.Migrator;

internal sealed class MigrationModelSnapshotVerifier
{
    private readonly IServiceProvider _serviceProvider;

    public MigrationModelSnapshotVerifier(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void VerifyNoPendingModelChanges()
    {
        using var scope = _serviceProvider.CreateScope();
        var issues = new List<string>();

        Verify<AuditLogDbContext>(scope.ServiceProvider, issues);
        Verify<ConfigurationDbContext>(scope.ServiceProvider, issues);
        Verify<IdentityDbContext>(scope.ServiceProvider, issues);
        Verify<NotificationDbContext>(scope.ServiceProvider, issues);
        Verify<StorageDbContext>(scope.ServiceProvider, issues);
        Verify<SubscriptionDbContext>(scope.ServiceProvider, issues);
        Verify<TestGenerationDbContext>(scope.ServiceProvider, issues);
        Verify<TestExecutionDbContext>(scope.ServiceProvider, issues);
        Verify<TestReportingDbContext>(scope.ServiceProvider, issues);
        Verify<ApiDocumentationDbContext>(scope.ServiceProvider, issues);
        Verify<LlmAssistantDbContext>(scope.ServiceProvider, issues);

        if (issues.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "EF Core migrations are not up to date. Resolve the following before building or starting Docker services:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, issues.Select(x => $"- {x}")));
    }

    private static void Verify<TContext>(IServiceProvider serviceProvider, List<string> issues)
        where TContext : DbContext
    {
        var context = serviceProvider.GetRequiredService<TContext>();
        var contextName = typeof(TContext).Name;

        if (!context.Database.GetMigrations().Any())
        {
            issues.Add($"{contextName}: no migrations were found in ClassifiedAds.Migrator.");
            return;
        }

        if (context.Database.HasPendingModelChanges())
        {
            issues.Add($"{contextName}: model changes exist without a matching migration.");
        }
    }
}
