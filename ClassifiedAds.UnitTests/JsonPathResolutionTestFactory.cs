using ClassifiedAds.Contracts.TestExecution.JsonPathResolution;

namespace ClassifiedAds.UnitTests;

public static class JsonPathResolutionTestFactory
{
    public static IJsonPathResolver CreateResolver()
        => new JsonPathResolver(new JsonPathResolutionOptions
        {
            WrapperNames = ["data", "result", "payload", "response", "items"],
            FieldAliases = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = ["_id"],
                ["_id"] = ["id"],
            },
        });
}
