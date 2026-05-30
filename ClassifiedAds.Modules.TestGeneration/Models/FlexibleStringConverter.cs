using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassifiedAds.Modules.TestGeneration.Models;

/// <summary>
/// Accepts a JSON string or raw JSON value and stores it as a string.
/// This keeps n8n/LLM callback payloads tolerant when request.body is emitted
/// as an object instead of the serialized JSON string expected by the domain.
/// </summary>
public sealed class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString();
        }

        using var document = JsonDocument.ParseValue(ref reader);
        return document.RootElement.GetRawText();
    }

    public override void Write(
        Utf8JsonWriter writer,
        string value,
        JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value);
    }
}
