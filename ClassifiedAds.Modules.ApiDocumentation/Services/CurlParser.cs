using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using System;
using System.Collections.Generic;
using System.Text;

namespace ClassifiedAds.Modules.ApiDocumentation.Services;

public class CurlParseResult
{
    public string Method { get; set; } = "GET";

    public string Url { get; set; }

    public string Path { get; set; }

    public string Host { get; set; }

    public Dictionary<string, string> Headers { get; set; } = new();

    public Dictionary<string, string> QueryParams { get; set; } = new();

    public string Body { get; set; }

    public string ContentType { get; set; }

    public string AuthType { get; set; }

    public string AuthCredentials { get; set; }
}

public static class CurlParser
{
    public static CurlParseResult Parse(string curlCommand)
    {
        if (string.IsNullOrWhiteSpace(curlCommand))
        {
            throw new ValidationException("Lệnh cURL là bắt buộc.");
        }

        // 1. Normalize: remove line continuations and trim
        var normalized = curlCommand
            .Replace("\\\r\n", " ")
            .Replace("\\\n", " ")
            .Replace("\\\r", " ")
            .Trim();

        if (!normalized.StartsWith("curl", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Lệnh phải bắt đầu bằng 'curl'.");
        }

        // 2. Tokenize with shell-aware quote handling
        var tokens = Tokenize(normalized);

        // 3. Parse tokens
        var result = new CurlParseResult();
        bool methodExplicitlySet = false;
        bool hasBody = false;

        for (int i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];

            if (token == "curl")
            {
                continue;
            }

            if (token == "-X" || token == "--request")
            {
                if (i + 1 < tokens.Count)
                {
                    result.Method = tokens[++i].ToUpperInvariant();
                    methodExplicitlySet = true;
                }

                continue;
            }

            if (token == "-H" || token == "--header")
            {
                if (i + 1 < tokens.Count)
                {
                    var headerValue = tokens[++i];
                    var colonIndex = headerValue.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var name = headerValue.Substring(0, colonIndex).Trim();
                        var value = headerValue.Substring(colonIndex + 1).Trim();
                        result.Headers[name] = value;

                        if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                        {
                            result.ContentType = value;
                        }
                    }
                }

                continue;
            }

            if (token == "-d" || token == "--data" || token == "--data-raw" || token == "--data-binary")
            {
                if (i + 1 < tokens.Count)
                {
                    var data = tokens[++i];
                    if (data.StartsWith("@"))
                    {
                        throw new ValidationException("Không hỗ trợ tham chiếu file (@file) trong lệnh cURL. Vui lòng dán trực tiếp nội dung dữ liệu.");
                    }

                    result.Body = data;
                    hasBody = true;
                }

                continue;
            }

            if (token == "-u" || token == "--user")
            {
                if (i + 1 < tokens.Count)
                {
                    result.AuthType = "Basic";
                    result.AuthCredentials = tokens[++i];
                }

                continue;
            }

            if (token == "-I" || token == "--head")
            {
                if (!methodExplicitlySet)
                {
                    result.Method = "HEAD";
                }

                continue;
            }

            // Skip known flags that take a value
            if (token == "-o" || token == "--output" ||
                token == "-A" || token == "--user-agent" ||
                token == "-e" || token == "--referer" ||
                token == "-b" || token == "--cookie" ||
                token == "--connect-timeout" ||
                token == "--max-time")
            {
                if (i + 1 < tokens.Count)
                {
                    i++; // skip value
                }

                continue;
            }

            // Skip known boolean flags
            if (token == "-s" || token == "--silent" ||
                token == "-S" || token == "--show-error" ||
                token == "-k" || token == "--insecure" ||
                token == "-L" || token == "--location" ||
                token == "-v" || token == "--verbose" ||
                token == "-i" || token == "--include" ||
                token == "--compressed" ||
                token.StartsWith("-"))
            {
                continue;
            }

            // Bare argument = URL
            if (string.IsNullOrEmpty(result.Url))
            {
                result.Url = token;
            }
        }

        // 4. Auto-switch to POST if body present and no method explicitly set
        if (hasBody && !methodExplicitlySet)
        {
            result.Method = "POST";
        }

        // 5. Parse URL
        if (string.IsNullOrEmpty(result.Url))
        {
            throw new ValidationException("Không tìm thấy URL trong lệnh cURL.");
        }

        if (Uri.TryCreate(result.Url, UriKind.Absolute, out var uri))
        {
            result.Host = uri.Host;
            result.Path = uri.AbsolutePath;

            // Parse query params
            if (!string.IsNullOrEmpty(uri.Query))
            {
                var query = uri.Query.TrimStart('?');
                foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
                {
                    var eqIndex = pair.IndexOf('=');
                    if (eqIndex > 0)
                    {
                        var key = Uri.UnescapeDataString(pair.Substring(0, eqIndex));
                        var value = Uri.UnescapeDataString(pair.Substring(eqIndex + 1));
                        result.QueryParams[key] = value;
                    }
                    else
                    {
                        result.QueryParams[Uri.UnescapeDataString(pair)] = string.Empty;
                    }
                }
            }
        }
        else
        {
            throw new ValidationException($"URL không hợp lệ: {result.Url}");
        }

        // Default Content-Type from body if not set
        if (hasBody && string.IsNullOrEmpty(result.ContentType))
        {
            result.ContentType = "application/x-www-form-urlencoded";
        }

        return result;
    }

    private static List<string> Tokenize(string input)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        bool inSingleQuote = false;
        bool inDoubleQuote = false;
        bool escaped = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (escaped)
            {
                current.Append(c);
                escaped = false;
                continue;
            }

            if (c == '\\' && !inSingleQuote)
            {
                escaped = true;
                continue;
            }

            if (c == '\'' && !inDoubleQuote)
            {
                inSingleQuote = !inSingleQuote;
                continue;
            }

            if (c == '"' && !inSingleQuote)
            {
                inDoubleQuote = !inDoubleQuote;
                continue;
            }

            if (char.IsWhiteSpace(c) && !inSingleQuote && !inDoubleQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
