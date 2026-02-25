using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.Modules.TestGeneration.Models;

public class TestSuiteScopeModel
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? ApiSpecId { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public GenerationType GenerationType { get; set; }

    public TestSuiteStatus Status { get; set; }

    public ApprovalStatus ApprovalStatus { get; set; }

    public List<Guid> SelectedEndpointIds { get; set; } = new();

    /// <summary>
    /// User-provided business rules per endpoint (plain text).
    /// </summary>
    public Dictionary<Guid, string> EndpointBusinessContexts { get; set; } = new();

    /// <summary>
    /// Global business rules that apply to all endpoints in this suite (free text).
    /// </summary>
    public string GlobalBusinessRules { get; set; }

    public int SelectedEndpointCount { get; set; }

    public Guid CreatedById { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }

    public string RowVersion { get; set; }

    public static TestSuiteScopeModel FromEntity(TestSuite suite)
    {
        return new TestSuiteScopeModel
        {
            Id = suite.Id,
            ProjectId = suite.ProjectId,
            ApiSpecId = suite.ApiSpecId,
            Name = suite.Name,
            Description = suite.Description,
            GenerationType = suite.GenerationType,
            Status = suite.Status,
            ApprovalStatus = suite.ApprovalStatus,
            SelectedEndpointIds = suite.SelectedEndpointIds ?? new List<Guid>(),
            EndpointBusinessContexts = suite.EndpointBusinessContexts ?? new Dictionary<Guid, string>(),
            GlobalBusinessRules = suite.GlobalBusinessRules,
            SelectedEndpointCount = suite.SelectedEndpointIds?.Count ?? 0,
            CreatedById = suite.CreatedById,
            CreatedDateTime = suite.CreatedDateTime,
            UpdatedDateTime = suite.UpdatedDateTime,
            RowVersion = suite.RowVersion != null ? Convert.ToBase64String(suite.RowVersion) : null,
        };
    }
}
