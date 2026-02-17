using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassifiedAds.Modules.ApiDocumentation.Models;

[JsonConverter(typeof(EndpointParameterDataTypeJsonConverter))]
public enum EndpointParameterDataType
{
    String = 0,
    Integer = 1,
    Number = 2,
    Boolean = 3,
    Object = 4,
    Array = 5,
    Uuid = 6,
}

public static class EndpointParameterDataTypeExtensions
{
    public static string ToStorageValue(this EndpointParameterDataType dataType)
    {
        return dataType switch
        {
            EndpointParameterDataType.String => "string",
            EndpointParameterDataType.Integer => "integer",
            EndpointParameterDataType.Number => "number",
            EndpointParameterDataType.Boolean => "boolean",
            EndpointParameterDataType.Object => "object",
            EndpointParameterDataType.Array => "array",
            EndpointParameterDataType.Uuid => "uuid",
            _ => "string",
        };
    }
}

public sealed class EndpointParameterDataTypeJsonConverter : JsonStringEnumConverter
{
    public EndpointParameterDataTypeJsonConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    {
    }
}
