using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Deserializes a <c>List&lt;string&gt;</c> whose elements may be
/// any JSON primitive (string, number, boolean, null).  The LLM occasionally
/// returns numbers or booleans in arrays (e.g. bodyContains: ["value", true, 42])
/// instead of quoted strings, which would otherwise throw during deserialization.
/// All non-null primitives are coerced to their string representation; null
/// values are skipped (not added to the list).
/// </summary>
public sealed class FlexibleStringListConverter : JsonConverter<List<string>>
{
    public override List<string> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException($"Expected StartArray token, got {reader.TokenType}.");
        }

        var result = new List<string>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return result;
            }

            var value = reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
                JsonTokenType.True => "true",
                JsonTokenType.False => "false",
                JsonTokenType.Null => null,
                _ => reader.GetString() ?? string.Empty,
            };

            if (value != null)
            {
                result.Add(value);
            }
        }

        throw new JsonException("Unexpected end of JSON while reading array.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<string> value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        foreach (var item in value)
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }
}
