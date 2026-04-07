using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using System;
using System.Text.Json;

namespace ClassifiedAds.Modules.ApiDocumentation.Services;

internal static class JsonbFieldNormalizer
{
    public static string NormalizeOptionalJson(string value, string fieldName, bool allowPlainText = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            return JsonSerializer.Serialize(document.RootElement);
        }
        catch (JsonException) when (allowPlainText && !LooksLikeJson(trimmed))
        {
            return JsonSerializer.Serialize(trimmed);
        }
        catch (JsonException)
        {
            throw new ValidationException($"{fieldName} phải là JSON hợp lệ.");
        }
    }

    public static bool TryExtractFirstString(string value, out string result)
    {
        result = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.String => TryAssign(document.RootElement.GetString(), out result),
                JsonValueKind.Array => TryExtractFromArray(document.RootElement, out result),
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => TryAssign(document.RootElement.GetRawText(), out result),
                _ => false,
            };
        }
        catch (JsonException)
        {
            var trimmed = value.Trim();
            if (LooksLikeJson(trimmed))
            {
                return false;
            }

            return TryAssign(trimmed.Trim('"'), out result);
        }
    }

    private static bool TryExtractFromArray(JsonElement arrayElement, out string result)
    {
        foreach (var item in arrayElement.EnumerateArray())
        {
            switch (item.ValueKind)
            {
                case JsonValueKind.String:
                    if (TryAssign(item.GetString(), out result))
                    {
                        return true;
                    }

                    break;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (TryAssign(item.GetRawText(), out result))
                    {
                        return true;
                    }

                    break;
            }
        }

        result = null;
        return false;
    }

    private static bool TryAssign(string value, out string result)
    {
        result = string.IsNullOrWhiteSpace(value) ? null : value;
        return result != null;
    }

    private static bool LooksLikeJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value[0] switch
        {
            '{' or '[' or '"' => true,
            '-' => true,
            >= '0' and <= '9' => true,
            _ => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "null", StringComparison.OrdinalIgnoreCase),
        };
    }
}
