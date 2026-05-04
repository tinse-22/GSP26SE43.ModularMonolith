using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// API view model for an LLM suggestion (FE-15).
/// </summary>
public class LlmSuggestionModel
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Guid Id { get; set; }
    public Guid TestSuiteId { get; set; }
    public Guid? EndpointId { get; set; }

    /// <summary>
    /// HTTP method resolved from SuggestedRequest (e.g. "GET", "POST").
    /// Avoids displaying raw EndpointId UUID on the FE.
    /// </summary>
    public string EndpointMethod { get; set; }

    /// <summary>
    /// Endpoint path resolved from SuggestedRequest (e.g. "/api/auth/register").
    /// Avoids displaying raw EndpointId UUID on the FE.
    /// </summary>
    public string EndpointPath { get; set; }

    /// <summary>
    /// True when the suggestion was generated with an SRS document linked to its TestSuite.
    /// </summary>
    public bool HasSrsContext { get; set; }

    /// <summary>
    /// Title of the SRS document used at generation time (null if no SRS context).
    /// </summary>
    public string SrsDocumentTitle { get; set; }

    /// <summary>
    /// UUIDs of SRS requirements the LLM reported this suggestion covers.
    /// </summary>
    public List<Guid> CoveredRequirementIds { get; set; } = new();

    /// <summary>
    /// Brief info (code + title) of each covered requirement, resolved from SrsRequirement table.
    /// </summary>
    public List<CoveredRequirementBriefModel> CoveredRequirements { get; set; } = new();
    public string CacheKey { get; set; }
    public int DisplayOrder { get; set; }
    public string SuggestionType { get; set; }
    public string TestType { get; set; }
    public string SuggestedName { get; set; }
    public string SuggestedDescription { get; set; }
    public string SuggestedRequest { get; set; }
    public string SuggestedExpectation { get; set; }
    public List<SuggestionVariableModel> SuggestedVariables { get; set; } = new();
    public List<string> SuggestedTags { get; set; } = new();
    public string Priority { get; set; }
    public string ReviewStatus { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string ReviewNotes { get; set; }
    public string ModifiedContent { get; set; }
    public Guid? AppliedTestCaseId { get; set; }
    public string LlmModel { get; set; }
    public int? TokensUsed { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
    public DateTimeOffset CreatedDateTime { get; set; }
    public DateTimeOffset? UpdatedDateTime { get; set; }
    public string RowVersion { get; set; }
    public LlmSuggestionFeedbackModel CurrentUserFeedback { get; set; }
    public LlmSuggestionFeedbackSummaryModel FeedbackSummary { get; set; }

    public static LlmSuggestionModel FromEntity(LlmSuggestion entity)
    {
        var req = TryDeserializeRequest(entity.SuggestedRequest);

        return new LlmSuggestionModel
        {
            Id = entity.Id,
            TestSuiteId = entity.TestSuiteId,
            EndpointId = entity.EndpointId,
            EndpointMethod = req?.HttpMethod,
            EndpointPath = req?.Url,
            HasSrsContext = entity.SrsDocumentId.HasValue,
            CoveredRequirementIds = DeserializeGuidList(entity.CoveredRequirementIds),
            CacheKey = entity.CacheKey,
            DisplayOrder = entity.DisplayOrder,
            SuggestionType = entity.SuggestionType.ToString(),
            TestType = entity.TestType.ToString(),
            SuggestedName = entity.SuggestedName,
            SuggestedDescription = entity.SuggestedDescription,
            SuggestedRequest = entity.SuggestedRequest,
            SuggestedExpectation = entity.SuggestedExpectation,
            SuggestedVariables = DeserializeVariables(entity.SuggestedVariables),
            SuggestedTags = DeserializeTags(entity.SuggestedTags),
            Priority = entity.Priority.ToString(),
            ReviewStatus = entity.ReviewStatus.ToString(),
            ReviewedById = entity.ReviewedById,
            ReviewedAt = entity.ReviewedAt,
            ReviewNotes = entity.ReviewNotes,
            ModifiedContent = entity.ModifiedContent,
            AppliedTestCaseId = entity.AppliedTestCaseId,
            LlmModel = entity.LlmModel,
            TokensUsed = entity.TokensUsed,
            IsDeleted = entity.IsDeleted,
            DeletedAt = entity.DeletedAt,
            DeletedById = entity.DeletedById,
            CreatedDateTime = entity.CreatedDateTime,
            UpdatedDateTime = entity.UpdatedDateTime,
            RowVersion = entity.RowVersion != null ? Convert.ToBase64String(entity.RowVersion) : null,
        };
    }

    private static N8nTestCaseRequest TryDeserializeRequest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<N8nTestCaseRequest>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    private static List<Guid> DeserializeGuidList(string json)
        => DeserializeGuidListStatic(json);

    public static List<Guid> DeserializeGuidListStatic(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Guid>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json, JsonOpts) ?? new List<Guid>();
        }
        catch
        {
            return new List<Guid>();
        }
    }

    private static List<string> DeserializeTags(string tagsJson)
    {
        if (string.IsNullOrWhiteSpace(tagsJson))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(tagsJson, JsonOpts) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static List<SuggestionVariableModel> DeserializeVariables(string variablesJson)
    {
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return new List<SuggestionVariableModel>();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<N8nTestCaseVariable>>(variablesJson, JsonOpts);
            if (items == null)
            {
                return new List<SuggestionVariableModel>();
            }

            var result = new List<SuggestionVariableModel>();
            foreach (var v in items)
            {
                result.Add(new SuggestionVariableModel
                {
                    VariableName = v.VariableName,
                    ExtractFrom = v.ExtractFrom,
                    JsonPath = v.JsonPath,
                    HeaderName = v.HeaderName,
                    Regex = v.Regex,
                    DefaultValue = v.DefaultValue,
                });
            }

            return result;
        }
        catch
        {
            return new List<SuggestionVariableModel>();
        }
    }
}

public class SuggestionVariableModel
{
    public string VariableName { get; set; }
    public string ExtractFrom { get; set; }
    public string JsonPath { get; set; }
    public string HeaderName { get; set; }
    public string Regex { get; set; }
    public string DefaultValue { get; set; }
}

public class CoveredRequirementBriefModel
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Title { get; set; }
}
