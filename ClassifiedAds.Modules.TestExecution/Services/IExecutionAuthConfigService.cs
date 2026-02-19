namespace ClassifiedAds.Modules.TestExecution.Services;

public interface IExecutionAuthConfigService
{
    /// <summary>
    /// Validates auth config against the selected auth type.
    /// Throws ValidationException if invalid.
    /// </summary>
    void ValidateAuthConfig(Models.ExecutionAuthConfigModel authConfig);

    /// <summary>
    /// Serializes auth config model to JSON string for storage.
    /// </summary>
    string SerializeAuthConfig(Models.ExecutionAuthConfigModel authConfig);

    /// <summary>
    /// Deserializes auth config from JSON string.
    /// </summary>
    Models.ExecutionAuthConfigModel DeserializeAuthConfig(string json);

    /// <summary>
    /// Returns a masked copy of auth config (sensitive fields replaced with "***").
    /// </summary>
    Models.ExecutionAuthConfigModel MaskAuthConfig(Models.ExecutionAuthConfigModel authConfig);
}
