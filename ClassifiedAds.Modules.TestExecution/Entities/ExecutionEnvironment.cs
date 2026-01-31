using ClassifiedAds.Domain.Entities;
using System;

namespace ClassifiedAds.Modules.TestExecution.Entities;

/// <summary>
/// Execution environment configuration for running tests.
/// </summary>
public class ExecutionEnvironment : Entity<Guid>, IAggregateRoot
{
    /// <summary>
    /// Project this environment belongs to.
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Environment name (e.g., Development, Staging, Production).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Base URL for API calls in this environment.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Environment variables as JSON.
    /// </summary>
    public string Variables { get; set; }

    /// <summary>
    /// Default headers as JSON.
    /// </summary>
    public string Headers { get; set; }

    /// <summary>
    /// Authentication configuration as JSON (encrypted).
    /// </summary>
    public string AuthConfig { get; set; }

    /// <summary>
    /// Whether this is the default environment for the project.
    /// </summary>
    public bool IsDefault { get; set; }
}
