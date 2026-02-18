using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.ApiDocumentation.Services;

public class PathParameterTemplateService : IPathParameterTemplateService
{
    private const int MaxPathParametersPerEndpoint = 10;
    private static readonly Regex PlaceholderRegex = new(@"\{([^{}]+)\}", RegexOptions.Compiled);
    private static readonly Regex ValidNameRegex = new(@"^\w+$", RegexOptions.Compiled);

    public List<PathParameterInfo> ExtractPathParameters(string pathTemplate)
    {
        if (string.IsNullOrWhiteSpace(pathTemplate))
        {
            return new List<PathParameterInfo>();
        }

        var matches = PlaceholderRegex.Matches(pathTemplate);
        var result = new List<PathParameterInfo>(matches.Count);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < matches.Count; i++)
        {
            var name = matches[i].Groups[1].Value;

            if (!ValidNameRegex.IsMatch(name))
            {
                throw new ValidationException(
                    $"Tên path parameter '{name}' không hợp lệ. " +
                    "Chỉ chấp nhận chữ cái, số và dấu gạch dưới.");
            }

            if (!seenNames.Add(name))
            {
                throw new ValidationException(
                    $"Tên path parameter trùng lặp: '{name}'. Mỗi placeholder phải có tên duy nhất.");
            }

            result.Add(new PathParameterInfo { Name = name, Position = i });
        }

        if (result.Count > MaxPathParametersPerEndpoint)
        {
            throw new ValidationException(
                $"Mỗi endpoint chỉ hỗ trợ tối đa {MaxPathParametersPerEndpoint} path parameters.");
        }

        return result;
    }

    public PathTemplateValidationResult ValidatePathParameterConsistency(
        string path, List<ManualParameterDefinition> parameters)
    {
        var extracted = ExtractPathParameters(path);

        var pathParams = (parameters ?? new List<ManualParameterDefinition>())
            .Where(p => string.Equals(p.Location, "Path", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var duplicateDefined = pathParams
            .Where(p => !string.IsNullOrWhiteSpace(p.Name))
            .GroupBy(p => p.Name.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        var extractedNames = new HashSet<string>(
            extracted.Select(e => e.Name), StringComparer.OrdinalIgnoreCase);
        var definedNames = new HashSet<string>(
            pathParams.Select(p => p.Name?.Trim()).Where(n => !string.IsNullOrEmpty(n)),
            StringComparer.OrdinalIgnoreCase);

        var errors = new List<string>();
        var autoCreated = new List<ManualParameterDefinition>();
        var warnings = new List<string>();

        if (duplicateDefined.Count > 0)
        {
            errors.Add(
                $"Path parameter bị trùng trong danh sách parameters: {string.Join(", ", duplicateDefined)}.");
        }

        // Check extra: defined Location=Path but not in template
        foreach (var p in pathParams)
        {
            var name = p.Name?.Trim();
            if (!string.IsNullOrEmpty(name) && !extractedNames.Contains(name))
            {
                var validNames = extractedNames.Count > 0
                    ? string.Join(", ", extractedNames)
                    : "Không có placeholder nào trong path";
                errors.Add(
                    $"Path parameter '{name}' không tồn tại trong path '{path}'. " +
                    $"Các placeholder hợp lệ: {validNames}.");
            }
        }

        // Auto-create missing: extracted but not defined
        foreach (var e in extracted)
        {
            if (!definedNames.Contains(e.Name))
            {
                autoCreated.Add(new ManualParameterDefinition
                {
                    Name = e.Name,
                    Location = "Path",
                    DataType = EndpointParameterDataType.String,
                    IsRequired = true,
                });
            }
        }

        // Enforce IsRequired = true for all path params
        foreach (var p in pathParams)
        {
            if (!p.IsRequired)
            {
                p.IsRequired = true;
                warnings.Add(
                    $"Path parameter '{p.Name}' đã được set IsRequired=true (path params luôn bắt buộc).");
            }
        }

        return new PathTemplateValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            AutoCreatedParams = autoCreated,
            Warnings = warnings,
        };
    }

    public List<ManualParameterDefinition> EnsurePathParameterConsistency(
        string path, List<ManualParameterDefinition> parameters)
    {
        var normalized = parameters ?? new List<ManualParameterDefinition>();

        // Validate parameter names
        var pathParams = normalized
            .Where(p => string.Equals(p.Location, "Path", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var p in pathParams)
        {
            var name = p.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                throw new ValidationException("Tên path parameter là bắt buộc.");
            }

            if (!ValidNameRegex.IsMatch(name))
            {
                throw new ValidationException(
                    $"Tên path parameter '{name}' không hợp lệ. " +
                    "Chỉ chấp nhận chữ cái, số và dấu gạch dưới.");
            }

            p.Name = name;
        }

        var result = ValidatePathParameterConsistency(path, normalized);

        if (!result.IsValid)
        {
            throw new ValidationException(string.Join(" ", result.Errors));
        }

        var merged = new List<ManualParameterDefinition>(normalized);
        merged.AddRange(result.AutoCreatedParams);

        return merged;
    }

    public ResolvedUrlResult ResolveUrl(string path, Dictionary<string, string> parameterValues)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new ResolvedUrlResult
            {
                OriginalTemplate = path ?? string.Empty,
                ResolvedUrl = path ?? string.Empty,
                IsFullyResolved = true,
            };
        }

        var extracted = ExtractPathParameters(path);

        if (extracted.Count == 0)
        {
            return new ResolvedUrlResult
            {
                OriginalTemplate = path,
                ResolvedUrl = path,
                IsFullyResolved = true,
            };
        }

        var resolvedPath = path;
        var resolvedParams = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var unresolvedParams = new List<string>();

        foreach (var param in extracted)
        {
            if (TryGetValueIgnoreCase(parameterValues, param.Name, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                resolvedPath = resolvedPath.Replace(
                    $"{{{param.Name}}}", Uri.EscapeDataString(value));
                resolvedParams[param.Name] = value;
            }
            else
            {
                unresolvedParams.Add(param.Name);
            }
        }

        return new ResolvedUrlResult
        {
            OriginalTemplate = path,
            ResolvedUrl = unresolvedParams.Count == 0 ? resolvedPath : null,
            ResolvedParameters = resolvedParams,
            UnresolvedParameters = unresolvedParams,
            IsFullyResolved = unresolvedParams.Count == 0,
        };
    }

    public List<PathParameterMutation> GenerateMutations(
        string parameterName, string dataType, string format, string defaultValue)
    {
        var mutations = new List<PathParameterMutation>();
        var normalizedType = (dataType ?? "string").ToLowerInvariant();
        var normalizedFormat = format?.ToLowerInvariant();

        // Common mutations applied to all types
        mutations.Add(new PathParameterMutation
        {
            MutationType = "empty",
            Label = $"{parameterName} - empty value",
            Value = string.Empty,
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' gửi giá trị rỗng.",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "specialChars",
            Label = $"{parameterName} - special characters",
            Value = "<script>alert(1)</script>",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' chứa ký tự đặc biệt/XSS payload.",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "sqlInjection",
            Label = $"{parameterName} - SQL injection attempt",
            Value = "1; DROP TABLE users--",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' chứa SQL injection payload.",
        });

        switch (normalizedType)
        {
            case "integer":
            case "int":
            case "long":
                AddIntegerMutations(mutations, parameterName, normalizedFormat);
                break;
            case "number":
            case "float":
            case "double":
            case "decimal":
                AddNumberMutations(mutations, parameterName);
                break;
            case "boolean":
            case "bool":
                AddBooleanMutations(mutations, parameterName);
                break;
            case "string":
            default:
                AddStringMutations(mutations, parameterName, normalizedFormat);
                break;
        }

        mutations.Add(new PathParameterMutation
        {
            MutationType = "nonExistent",
            Label = $"{parameterName} - non-existent resource",
            Value = GenerateNonExistentValue(normalizedType, normalizedFormat),
            ExpectedStatusCode = 404,
            Description = $"Path parameter '{parameterName}' trỏ tới resource không tồn tại.",
        });

        return mutations;
    }

    private void AddIntegerMutations(
        List<PathParameterMutation> mutations, string parameterName, string format)
    {
        mutations.Add(new PathParameterMutation
        {
            MutationType = "wrongType",
            Label = $"{parameterName} - wrong type (text instead of integer)",
            Value = "abc",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' gửi text thay vì số nguyên.",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "boundary_zero",
            Label = $"{parameterName} - boundary: zero",
            Value = "0",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' = 0 (thường không hợp lệ cho ID).",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "boundary_negative",
            Label = $"{parameterName} - boundary: negative",
            Value = "-1",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' = -1 (giá trị âm).",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "boundary_float",
            Label = $"{parameterName} - float instead of integer",
            Value = "1.5",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' gửi số thực thay vì số nguyên.",
        });

        if (format == "int32")
        {
            mutations.Add(new PathParameterMutation
            {
                MutationType = "boundary_max_int32",
                Label = $"{parameterName} - max int32",
                Value = "2147483647",
                ExpectedStatusCode = 200,
                Description = $"Path parameter '{parameterName}' = Int32.MaxValue (boundary test).",
            });

            mutations.Add(new PathParameterMutation
            {
                MutationType = "boundary_overflow_int32",
                Label = $"{parameterName} - overflow int32",
                Value = "2147483648",
                ExpectedStatusCode = 400,
                Description = $"Path parameter '{parameterName}' = Int32.MaxValue + 1 (overflow).",
            });
        }
        else if (string.IsNullOrWhiteSpace(format) || format == "int64")
        {
            mutations.Add(new PathParameterMutation
            {
                MutationType = "boundary_max_int64",
                Label = $"{parameterName} - max int64",
                Value = "9223372036854775807",
                ExpectedStatusCode = 200,
                Description = $"Path parameter '{parameterName}' = Int64.MaxValue (boundary test).",
            });
        }
    }

    private void AddNumberMutations(List<PathParameterMutation> mutations, string parameterName)
    {
        mutations.Add(new PathParameterMutation
        {
            MutationType = "wrongType",
            Label = $"{parameterName} - wrong type (text instead of number)",
            Value = "abc",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' gửi text thay vì số.",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "boundary_zero",
            Label = $"{parameterName} - boundary: zero",
            Value = "0",
            ExpectedStatusCode = 200,
            Description = $"Path parameter '{parameterName}' = 0.",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "boundary_negative",
            Label = $"{parameterName} - boundary: negative",
            Value = "-1.0",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' = -1.0 (giá trị âm).",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "boundary_veryLarge",
            Label = $"{parameterName} - very large number",
            Value = "999999999.999",
            ExpectedStatusCode = 200,
            Description = $"Path parameter '{parameterName}' = giá trị rất lớn.",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "boundary_verySmall",
            Label = $"{parameterName} - very small number",
            Value = "0.0000001",
            ExpectedStatusCode = 200,
            Description = $"Path parameter '{parameterName}' = giá trị rất nhỏ.",
        });
    }

    private void AddBooleanMutations(List<PathParameterMutation> mutations, string parameterName)
    {
        mutations.Add(new PathParameterMutation
        {
            MutationType = "wrongType",
            Label = $"{parameterName} - wrong type (not boolean)",
            Value = "abc",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' gửi giá trị không phải boolean.",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "numericBoolean",
            Label = $"{parameterName} - invalid numeric boolean",
            Value = "2",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' gửi số 2 (chỉ 0/1 hợp lệ cho boolean).",
        });
    }

    private void AddStringMutations(
        List<PathParameterMutation> mutations, string parameterName, string format)
    {
        if (format == "uuid")
        {
            mutations.Add(new PathParameterMutation
            {
                MutationType = "invalidFormat",
                Label = $"{parameterName} - invalid UUID format",
                Value = "not-a-uuid",
                ExpectedStatusCode = 400,
                Description = $"Path parameter '{parameterName}' gửi chuỗi không phải UUID.",
            });

            mutations.Add(new PathParameterMutation
            {
                MutationType = "partialUuid",
                Label = $"{parameterName} - partial UUID",
                Value = "550e8400-e29b-41d4",
                ExpectedStatusCode = 400,
                Description = $"Path parameter '{parameterName}' gửi UUID không đầy đủ.",
            });

            mutations.Add(new PathParameterMutation
            {
                MutationType = "allZerosUuid",
                Label = $"{parameterName} - all-zeros UUID",
                Value = "00000000-0000-0000-0000-000000000000",
                ExpectedStatusCode = 404,
                Description = $"Path parameter '{parameterName}' gửi UUID zero (resource không tồn tại).",
            });

            return;
        }

        if (format == "email")
        {
            mutations.Add(new PathParameterMutation
            {
                MutationType = "invalidFormat",
                Label = $"{parameterName} - invalid email",
                Value = "not-an-email",
                ExpectedStatusCode = 400,
                Description = $"Path parameter '{parameterName}' không phải email hợp lệ.",
            });

            mutations.Add(new PathParameterMutation
            {
                MutationType = "missingDomain",
                Label = $"{parameterName} - email missing domain",
                Value = "user@",
                ExpectedStatusCode = 400,
                Description = $"Path parameter '{parameterName}' là email thiếu domain.",
            });

            return;
        }

        mutations.Add(new PathParameterMutation
        {
            MutationType = "veryLong",
            Label = $"{parameterName} - very long string (500 chars)",
            Value = new string('a', 500),
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' gửi chuỗi dài 500 ký tự.",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "unicode",
            Label = $"{parameterName} - unicode characters",
            Value = "用户名",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' gửi ký tự Unicode CJK.",
        });

        mutations.Add(new PathParameterMutation
        {
            MutationType = "whitespace",
            Label = $"{parameterName} - whitespace only",
            Value = "   ",
            ExpectedStatusCode = 400,
            Description = $"Path parameter '{parameterName}' chỉ chứa khoảng trắng.",
        });
    }

    private string GenerateNonExistentValue(string normalizedType, string normalizedFormat)
    {
        return (normalizedType, normalizedFormat) switch
        {
            ("integer" or "int" or "long", _) => "999999999",
            ("number" or "float" or "double" or "decimal", _) => "999999999.99",
            ("string", "uuid") => "00000000-0000-0000-0000-000000000000",
            _ => "nonexistent-resource-id-99999",
        };
    }

    private static bool TryGetValueIgnoreCase(
        Dictionary<string, string> parameterValues, string key, out string value)
    {
        value = null;

        if (parameterValues == null || string.IsNullOrEmpty(key))
        {
            return false;
        }

        if (parameterValues.TryGetValue(key, out value))
        {
            return true;
        }

        foreach (var item in parameterValues)
        {
            if (string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = item.Value;
                return true;
            }
        }

        return false;
    }
}
