using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class ExecutionEnvironmentRuntimeResolver : IExecutionEnvironmentRuntimeResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IExecutionAuthConfigService _authConfigService;
    private readonly ILogger<ExecutionEnvironmentRuntimeResolver> _logger;

    public ExecutionEnvironmentRuntimeResolver(
        IHttpClientFactory httpClientFactory,
        IExecutionAuthConfigService authConfigService,
        ILogger<ExecutionEnvironmentRuntimeResolver> logger)
    {
        _httpClientFactory = httpClientFactory;
        _authConfigService = authConfigService;
        _logger = logger;
    }

    public async Task<ResolvedExecutionEnvironment> ResolveAsync(ExecutionEnvironment environment, CancellationToken ct = default)
    {
        var variables = DeserializeDictionary(environment.Variables);
        var headers = DeserializeDictionary(environment.Headers);
        var authConfig = _authConfigService.DeserializeAuthConfig(environment.AuthConfig);

        var resolved = new ResolvedExecutionEnvironment
        {
            EnvironmentId = environment.Id,
            Name = environment.Name,
            BaseUrl = environment.BaseUrl?.TrimEnd('/'),
            Variables = variables,
            DefaultHeaders = new Dictionary<string, string>(headers, StringComparer.OrdinalIgnoreCase),
            DefaultQueryParams = new Dictionary<string, string>(),
        };

        if (authConfig != null)
        {
            await ResolveAuth(authConfig, resolved, ct);
        }

        return resolved;
    }

    private async Task ResolveAuth(ExecutionAuthConfigModel authConfig, ResolvedExecutionEnvironment resolved, CancellationToken ct)
    {
        switch (authConfig.AuthType)
        {
            case AuthType.None:
                break;

            case AuthType.BearerToken:
                {
                    var headerName = string.IsNullOrWhiteSpace(authConfig.HeaderName) ? "Authorization" : authConfig.HeaderName;
                    if (!resolved.DefaultHeaders.ContainsKey(headerName))
                    {
                        resolved.DefaultHeaders[headerName] = $"Bearer {authConfig.Token}";
                    }

                    break;
                }

            case AuthType.Basic:
                {
                    var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{authConfig.Username}:{authConfig.Password}"));
                    if (!resolved.DefaultHeaders.ContainsKey("Authorization"))
                    {
                        resolved.DefaultHeaders["Authorization"] = $"Basic {credentials}";
                    }

                    break;
                }

            case AuthType.ApiKey:
                {
                    if (authConfig.ApiKeyLocation == ApiKeyLocation.Header)
                    {
                        if (!resolved.DefaultHeaders.ContainsKey(authConfig.ApiKeyName))
                        {
                            resolved.DefaultHeaders[authConfig.ApiKeyName] = authConfig.ApiKeyValue;
                        }
                    }
                    else
                    {
                        if (!resolved.DefaultQueryParams.ContainsKey(authConfig.ApiKeyName))
                        {
                            resolved.DefaultQueryParams[authConfig.ApiKeyName] = authConfig.ApiKeyValue;
                        }
                    }

                    break;
                }

            case AuthType.OAuth2ClientCredentials:
                {
                    var token = await ResolveOAuth2TokenAsync(authConfig, ct);
                    if (!string.IsNullOrEmpty(token) && !resolved.DefaultHeaders.ContainsKey("Authorization"))
                    {
                        resolved.DefaultHeaders["Authorization"] = $"Bearer {token}";
                    }

                    break;
                }
        }
    }

    private async Task<string> ResolveOAuth2TokenAsync(ExecutionAuthConfigModel authConfig, CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient("TestExecution");
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = authConfig.ClientId,
            ["client_secret"] = authConfig.ClientSecret,
        };

        if (authConfig.Scopes != null && authConfig.Scopes.Length > 0)
        {
            form["scope"] = string.Join(" ", authConfig.Scopes);
        }

        using var content = new FormUrlEncodedContent(form);
        using var response = await client.PostAsync(authConfig.TokenUrl, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OAuth2 token request failed. Status={Status}, TokenUrl={TokenUrl}",
                response.StatusCode, authConfig.TokenUrl);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("access_token", out var tokenElement))
        {
            return tokenElement.GetString();
        }

        _logger.LogWarning("OAuth2 token response missing access_token field. TokenUrl={TokenUrl}", authConfig.TokenUrl);
        return null;
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
