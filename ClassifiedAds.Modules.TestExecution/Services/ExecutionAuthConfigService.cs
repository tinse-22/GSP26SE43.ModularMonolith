using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Text.Json;

namespace ClassifiedAds.Modules.TestExecution.Services;

public class ExecutionAuthConfigService : IExecutionAuthConfigService
{
    private const string MaskedValue = "******";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public void ValidateAuthConfig(ExecutionAuthConfigModel authConfig)
    {
        if (authConfig == null)
        {
            return;
        }

        switch (authConfig.AuthType)
        {
            case AuthType.None:
                break;

            case AuthType.BearerToken:
                if (string.IsNullOrWhiteSpace(authConfig.Token))
                {
                    throw new ValidationException("Token là bắt buộc cho xác thực BearerToken.");
                }

                break;

            case AuthType.Basic:
                if (string.IsNullOrWhiteSpace(authConfig.Username))
                {
                    throw new ValidationException("Username là bắt buộc cho xác thực Basic.");
                }

                if (string.IsNullOrWhiteSpace(authConfig.Password))
                {
                    throw new ValidationException("Password là bắt buộc cho xác thực Basic.");
                }

                break;

            case AuthType.ApiKey:
                if (string.IsNullOrWhiteSpace(authConfig.ApiKeyName))
                {
                    throw new ValidationException("ApiKeyName là bắt buộc cho xác thực ApiKey.");
                }

                if (string.IsNullOrWhiteSpace(authConfig.ApiKeyValue))
                {
                    throw new ValidationException("ApiKeyValue là bắt buộc cho xác thực ApiKey.");
                }

                break;

            case AuthType.OAuth2ClientCredentials:
                if (string.IsNullOrWhiteSpace(authConfig.TokenUrl))
                {
                    throw new ValidationException("TokenUrl là bắt buộc cho xác thực OAuth2.");
                }

                if (string.IsNullOrWhiteSpace(authConfig.ClientId))
                {
                    throw new ValidationException("ClientId là bắt buộc cho xác thực OAuth2.");
                }

                if (string.IsNullOrWhiteSpace(authConfig.ClientSecret))
                {
                    throw new ValidationException("ClientSecret là bắt buộc cho xác thực OAuth2.");
                }

                break;

            default:
                throw new ValidationException($"AuthType '{authConfig.AuthType}' không được hỗ trợ.");
        }
    }

    public string SerializeAuthConfig(ExecutionAuthConfigModel authConfig)
    {
        if (authConfig == null)
        {
            return null;
        }

        return JsonSerializer.Serialize(authConfig, JsonOptions);
    }

    public ExecutionAuthConfigModel DeserializeAuthConfig(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ExecutionAuthConfigModel>(json, JsonOptions);
    }

    public ExecutionAuthConfigModel MaskAuthConfig(ExecutionAuthConfigModel authConfig)
    {
        if (authConfig == null)
        {
            return null;
        }

        return new ExecutionAuthConfigModel
        {
            AuthType = authConfig.AuthType,
            HeaderName = authConfig.HeaderName,
            Token = MaskSecret(authConfig.Token),
            Username = authConfig.Username,
            Password = MaskSecret(authConfig.Password),
            ApiKeyName = authConfig.ApiKeyName,
            ApiKeyValue = MaskSecret(authConfig.ApiKeyValue),
            ApiKeyLocation = authConfig.ApiKeyLocation,
            TokenUrl = authConfig.TokenUrl,
            ClientId = authConfig.ClientId,
            ClientSecret = MaskSecret(authConfig.ClientSecret),
            Scopes = authConfig.Scopes,
        };
    }

    private static string MaskSecret(string value)
    {
        return string.IsNullOrEmpty(value) ? value : MaskedValue;
    }
}
