using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestExecution.Models;

public class ExecutionEnvironmentModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public string Name { get; set; }

    public string BaseUrl { get; set; }

    public Dictionary<string, string> Variables { get; set; } = new();

    public Dictionary<string, string> Headers { get; set; } = new();

    public ExecutionAuthConfigModel AuthConfig { get; set; }

    public bool IsDefault { get; set; }

    public DateTimeOffset CreatedDateTime { get; set; }

    public DateTimeOffset? UpdatedDateTime { get; set; }

    public string RowVersion { get; set; }

    public static ExecutionEnvironmentModel FromEntity(ExecutionEnvironment env, IExecutionAuthConfigService authConfigService)
    {
        var authConfig = authConfigService.DeserializeAuthConfig(env.AuthConfig);
        var maskedAuthConfig = authConfigService.MaskAuthConfig(authConfig);

        return new ExecutionEnvironmentModel
        {
            Id = env.Id,
            ProjectId = env.ProjectId,
            Name = env.Name,
            BaseUrl = env.BaseUrl,
            Variables = DeserializeDictionary(env.Variables),
            Headers = DeserializeDictionary(env.Headers),
            AuthConfig = maskedAuthConfig,
            IsDefault = env.IsDefault,
            CreatedDateTime = env.CreatedDateTime,
            UpdatedDateTime = env.UpdatedDateTime,
            RowVersion = env.RowVersion != null ? Convert.ToBase64String(env.RowVersion) : null,
        };
    }

    private static Dictionary<string, string> DeserializeDictionary(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new Dictionary<string, string>();
    }
}
