using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Deserializes a <c>Dictionary&lt;string, string&gt;</c> whose values may be
/// any JSON primitive (string, number, boolean, null).  The LLM occasionally
/// returns numbers or booleans as dictionary values (e.g. <c>"$.data.price": 29.99</c>)
/// instead of quoted strings, which would otherwise throw during deserialization.
/// All non-null primitives are coerced to their string representation; null
/// values are stored as the empty string.
/// </summary>
public sealed class FlexibleStringDictionaryConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected StartObject token, got {reader.TokenType}.");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return result;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Expected PropertyName token, got {reader.TokenType}.");
            }

            var key = reader.GetString();
            reader.Read();

            string value;
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    value = reader.GetString();
                    break;
                case JsonTokenType.Number:
                    value = reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture);
                    break;
                case JsonTokenType.True:
                    value = "true";
                    break;
                case JsonTokenType.False:
                    value = "false";
                    break;
                case JsonTokenType.Null:
                    value = string.Empty;
                    break;
                case JsonTokenType.StartObject:
                case JsonTokenType.StartArray:
                    using (var doc = JsonDocument.ParseValue(ref reader))
                    {
                        value = doc.RootElement.GetRawText();
                    }
                    break;
                default:
                    value = string.Empty;
                    break;
            }

            result[key] = value;
        }

        throw new JsonException("Unexpected end of JSON while reading dictionary.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, string> value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();
        foreach (var kvp in value)
        {
            writer.WriteString(kvp.Key, kvp.Value);
        }

        writer.WriteEndObject();
    }
}
