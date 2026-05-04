using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public sealed class ExpectationResolver : IExpectationResolver
{
    private static readonly Regex StatusCodeRegex = new(@"\b([1-5]\d\d)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly string[] ValidationKeywords =
    {
        "validation failed", "invalid", "required", "duplicate", "already exists", "conflict", "unauthorized",
        "forbidden", "not found", "password", "email", "category", "price", "stock", "categoryid", "fielderrors",
        "formerrors", "token", "success", "registered successfully", "route not found"
    };

    public ResolvedExpectation Resolve(GeneratedScenarioContext context)
    {
        var srs = TryResolveFromSrs(context);
        if (srs != null)
        {
            return srs;
        }

        var swagger = TryResolveFromSwagger(context);
        if (swagger != null)
        {
            return swagger;
        }

        var llm = TryResolveFromLlm(context);
        if (llm != null)
        {
            return llm;
        }

        return BuildDefault(context);
    }

    public N8nTestCaseExpectation ResolveToN8nExpectation(GeneratedScenarioContext context)
    {
        var resolved = Resolve(context);
        return ToN8nExpectation(resolved);
    }

    public static N8nTestCaseExpectation ToN8nExpectation(ResolvedExpectation resolved)
    {
        if (resolved == null)
        {
            return new N8nTestCaseExpectation { ExpectedStatus = new List<int> { 200 } };
        }

        return new N8nTestCaseExpectation
        {
            ExpectedStatus = resolved.ExpectedStatusCodes?.Count > 0 ? resolved.ExpectedStatusCodes : new List<int> { 200 },
            ResponseSchema = resolved.ResponseSchema,
            HeaderChecks = resolved.HeaderChecks ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BodyContains = resolved.BodyContains ?? new List<string>(),
            BodyNotContains = resolved.BodyNotContains ?? new List<string>(),
            JsonPathChecks = resolved.JsonPathChecks ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            MaxResponseTime = resolved.MaxResponseTime,
            ExpectationSource = resolved.Source.ToString(),
            RequirementCode = resolved.RequirementCode,
            PrimaryRequirementId = resolved.PrimaryRequirementId,
        };
    }

    private static ResolvedExpectation TryResolveFromSrs(GeneratedScenarioContext context)
    {
        var requirements = RankRequirements(context).ToList();
        if (requirements.Count == 0)
        {
            return null;
        }

        foreach (var requirement in requirements)
        {
            foreach (var constraint in ParseConstraints(requirement))
            {
                var statuses = NormalizeStatuses(ParseStatusCodes(constraint.ExpectedOutcome, constraint.Constraint), context.TestType);
                if (statuses.Count == 0)
                {
                    continue;
                }

                var bodyContains = ExtractBodyContains(requirement, constraint, context.TestType);
                var bodyNotContains = ExtractBodyNotContains(requirement, constraint.Constraint);
                var jsonPathChecks = BuildSrsJsonPathChecks(requirement, constraint, context.TestType, statuses);

                return new ResolvedExpectation
                {
                    ExpectedStatusCodes = statuses,
                    BodyContains = bodyContains,
                    BodyNotContains = bodyNotContains,
                    JsonPathChecks = jsonPathChecks,
                    Source = ExpectationSource.Srs,
                    PrimaryRequirementId = requirement.Id,
                    RequirementCode = requirement.RequirementCode,
                };
            }
        }

        return null;
    }

    private static ResolvedExpectation TryResolveFromSwagger(GeneratedScenarioContext context)
    {
        var responses = context?.SwaggerResponses?.ToList();
        if (responses == null || responses.Count == 0)
        {
            return null;
        }

        var statuses = context.TestType == TestType.HappyPath
            ? responses.Where(x => x.StatusCode >= 200 && x.StatusCode <= 299).Select(x => x.StatusCode).Distinct().OrderBy(x => x).ToList()
            : responses.Where(x => x.StatusCode >= 400 && x.StatusCode <= 599).Select(x => x.StatusCode).Distinct().OrderBy(x => x).ToList();

        if (statuses.Count == 0)
        {
            return null;
        }

        var primaryResponse = responses.FirstOrDefault(x => x.StatusCode == statuses[0])
            ?? responses.FirstOrDefault(x => x.StatusCode >= 400 && x.StatusCode <= 599)
            ?? responses.FirstOrDefault(x => x.StatusCode >= 200 && x.StatusCode <= 299);

        var bodyContains = !string.IsNullOrWhiteSpace(primaryResponse?.Schema)
            ? ErrorResponseSchemaAnalyzer.ExtractFieldNames(primaryResponse.Schema)
            : new List<string>();
        var jsonPathChecks = !string.IsNullOrWhiteSpace(primaryResponse?.Schema)
            ? ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(primaryResponse.Schema, context.TestType)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (bodyContains.Count == 0 && jsonPathChecks.Count == 0)
        {
            var llmFallback = NormalizeLlmExpectation(context?.LlmExpectation, context?.TestType ?? TestType.Negative);
            if (llmFallback != null)
            {
                bodyContains = llmFallback.BodyContains;
                jsonPathChecks = llmFallback.JsonPathChecks;
            }
        }

        return new ResolvedExpectation
        {
            ExpectedStatusCodes = statuses,
            ResponseSchema = primaryResponse?.Schema,
            BodyContains = bodyContains,
            JsonPathChecks = jsonPathChecks,
            Source = ExpectationSource.Swagger,
        };
    }

    private static ResolvedExpectation TryResolveFromLlm(GeneratedScenarioContext context)
    {
        var normalized = NormalizeLlmExpectation(context?.LlmExpectation, context?.TestType ?? TestType.Negative);
        if (normalized == null)
        {
            return null;
        }

        return new ResolvedExpectation
        {
            ExpectedStatusCodes = normalized.ExpectedStatusCodes,
            ResponseSchema = normalized.ResponseSchema,
            HeaderChecks = normalized.HeaderChecks,
            BodyContains = normalized.BodyContains,
            BodyNotContains = normalized.BodyNotContains,
            JsonPathChecks = normalized.JsonPathChecks,
            MaxResponseTime = normalized.MaxResponseTime,
            Source = ExpectationSource.Llm,
            PrimaryRequirementId = normalized.PrimaryRequirementId,
            RequirementCode = normalized.RequirementCode,
        };
    }

    private static ResolvedExpectation NormalizeLlmExpectation(N8nTestCaseExpectation expectation, TestType testType)
    {
        if (expectation == null)
        {
            return null;
        }

        var statuses = NormalizeStatuses(expectation.ExpectedStatus?.ToList() ?? new List<int>(), testType);
        var bodyContains = expectation.BodyContains?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        var bodyNotContains = expectation.BodyNotContains?.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>();
        var jsonPathChecks = expectation.JsonPathChecks?.Count > 0
            ? new Dictionary<string, string>(expectation.JsonPathChecks, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var headerChecks = expectation.HeaderChecks?.Count > 0
            ? new Dictionary<string, string>(expectation.HeaderChecks, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (statuses.Count == 0 && bodyContains.Count == 0 && bodyNotContains.Count == 0 && jsonPathChecks.Count == 0 && headerChecks.Count == 0)
        {
            return null;
        }

        return new ResolvedExpectation
        {
            ExpectedStatusCodes = statuses,
            ResponseSchema = expectation.ResponseSchema,
            HeaderChecks = headerChecks,
            BodyContains = bodyContains,
            BodyNotContains = bodyNotContains,
            JsonPathChecks = jsonPathChecks,
            MaxResponseTime = expectation.MaxResponseTime,
            RequirementCode = expectation.RequirementCode,
            PrimaryRequirementId = expectation.PrimaryRequirementId,
        };
    }

    private static ResolvedExpectation BuildDefault(GeneratedScenarioContext context)
    {
        var statuses = NormalizeStatuses(context?.PreferredDefaultStatuses?.ToList() ?? new List<int>(), context?.TestType ?? TestType.Negative);
        if (statuses.Count == 0)
        {
            statuses = GetDefaultStatuses(context?.TestType ?? TestType.Negative, context?.HttpMethod);
        }

        return new ResolvedExpectation
        {
            ExpectedStatusCodes = statuses,
            Source = ExpectationSource.Default,
        };
    }

    private static IEnumerable<SrsRequirement> RankRequirements(GeneratedScenarioContext context)
    {
        if (context?.SrsRequirements == null || context.SrsRequirements.Count == 0)
        {
            return Enumerable.Empty<SrsRequirement>();
        }

        var coveredIds = new HashSet<Guid>(context.CoveredRequirementIds ?? Array.Empty<Guid>());
        return context.SrsRequirements
            .Where(x => x.IsReviewed)
            .Where(x => !x.EndpointId.HasValue || x.EndpointId == context.EndpointId)
            .OrderByDescending(x => coveredIds.Contains(x.Id))
            .ThenByDescending(x => x.EndpointId == context.EndpointId)
            .ThenBy(x => x.DisplayOrder)
            .ThenBy(x => x.RequirementCode);
    }

    private static List<SrsConstraintCandidate> ParseConstraints(SrsRequirement requirement)
    {
        var json = !string.IsNullOrWhiteSpace(requirement?.RefinedConstraints)
            ? requirement.RefinedConstraints
            : requirement?.TestableConstraints;
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<SrsConstraintCandidate>();
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
            {
                return new List<SrsConstraintCandidate>();
            }

            var result = new List<SrsConstraintCandidate>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                var constraint = item.TryGetProperty("constraint", out var c) ? c.GetString()?.Trim() : null;
                if (string.IsNullOrWhiteSpace(constraint))
                {
                    continue;
                }

                var expectedOutcome = item.TryGetProperty("expectedOutcome", out var o)
                    ? o.GetString()
                    : ExtractExpectedOutcome(constraint);

                result.Add(new SrsConstraintCandidate
                {
                    Constraint = constraint,
                    ExpectedOutcome = expectedOutcome,
                    Field = item.TryGetProperty("field", out var field) ? field.GetString() : InferFieldName(constraint),
                    RuleType = item.TryGetProperty("ruleType", out var ruleType) ? ruleType.GetString() : InferRuleType(constraint),
                });
            }

            return result;
        }
        catch
        {
            return new List<SrsConstraintCandidate>();
        }
    }

    private static string ExtractExpectedOutcome(string constraintText)
    {
        if (string.IsNullOrWhiteSpace(constraintText))
        {
            return null;
        }

        var arrowIndex = constraintText.IndexOf('→');
        if (arrowIndex < 0)
        {
            arrowIndex = constraintText.IndexOf("->", StringComparison.Ordinal);
        }

        return arrowIndex >= 0 && arrowIndex < constraintText.Length - 1
            ? constraintText[(arrowIndex + 1)..].Trim()
            : null;
    }

    private static List<int> ParseStatusCodes(string expectedOutcome, string constraintText)
    {
        var raw = string.Join(" ", new[] { expectedOutcome, constraintText }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return StatusCodeRegex.Matches(raw)
            .Select(x => int.TryParse(x.Groups[1].Value, out var code) ? code : 0)
            .Where(code => code >= 100 && code <= 599)
            .Distinct()
            .ToList();
    }

    private static List<int> NormalizeStatuses(List<int> statuses, TestType testType)
    {
        var filtered = (statuses ?? new List<int>())
            .Where(code => code >= 100 && code <= 599)
            .Distinct()
            .ToList();

        if (testType == TestType.HappyPath)
        {
            filtered = filtered.Where(code => code >= 200 && code <= 299).ToList();
        }
        else
        {
            filtered = filtered.Where(code => code < 200 || code >= 300).ToList();
        }

        return filtered;
    }

    private static List<string> ExtractBodyContains(SrsRequirement requirement, SrsConstraintCandidate constraint, TestType testType)
    {
        var raw = string.Join(" ", new[] { requirement?.Title, requirement?.Description, constraint?.Constraint, constraint?.ExpectedOutcome }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        var lower = raw.ToLowerInvariant();
        var result = new List<string>();
        var field = constraint?.Field;

        void Add(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && !result.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(value);
            }
        }

        if (!string.IsNullOrWhiteSpace(field) && !string.Equals(field, "request", StringComparison.OrdinalIgnoreCase))
        {
            Add(field);
        }

        if (constraint?.RuleType == "required")
        {
            Add("required");
        }

        if (constraint?.RuleType == "format")
        {
            Add("invalid");
        }

        if (constraint?.RuleType == "uniqueness")
        {
            Add("already exists");
        }

        if (constraint?.RuleType == "authorization")
        {
            Add(lower.Contains("forbidden") ? "forbidden" : "unauthorized");
        }

        if (lower.Contains("validation failed"))
        {
            Add("Validation failed");
        }

        if (lower.Contains("registered successfully"))
        {
            Add("registered successfully");
        }

        if (lower.Contains("not found"))
        {
            Add("not found");
        }

        if (lower.Contains("conflict"))
        {
            Add("conflict");
        }

        if (lower.Contains("minimum"))
        {
            Add("minimum");
        }

        if (lower.Contains("maximum"))
        {
            Add("maximum");
        }

        if (lower.Contains("lowercase"))
        {
            Add("lowercase");
        }

        if (result.Count == 0 && testType != TestType.HappyPath)
        {
            Add("error");
        }

        return result.Take(3).ToList();
    }

    private static List<string> ExtractBodyNotContains(SrsRequirement requirement, string constraintText)
    {
        var raw = string.Join(" ", new[] { requirement?.Title, requirement?.Description, constraintText }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }

        var lower = raw.ToLowerInvariant();
        var result = new List<string>();
        if ((lower.Contains("must not") || lower.Contains("không") || lower.Contains("not return") || lower.Contains("do not return"))
            && lower.Contains("password"))
        {
            result.Add("password");
        }

        return result;
    }

    private static Dictionary<string, string> BuildSrsJsonPathChecks(SrsRequirement requirement, SrsConstraintCandidate constraint, TestType testType, IReadOnlyList<int> statuses)
    {
        var raw = string.Join(" ", new[] { requirement?.Title, requirement?.Description, constraint?.Constraint, constraint?.ExpectedOutcome }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var lower = raw.ToLowerInvariant();
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var isSuccess = statuses != null && statuses.Any(code => code is >= 200 and < 300);

        result["$.success"] = isSuccess ? "true" : "false";

        if (!isSuccess)
        {
            if (!string.IsNullOrWhiteSpace(constraint?.Field) && !string.Equals(constraint.Field, "request", StringComparison.OrdinalIgnoreCase))
            {
                result["$.errors.*"] = constraint.Field;
            }
            else if (constraint?.RuleType == "authorization")
            {
                result["$.message"] = lower.Contains("forbidden") ? "forbidden" : "unauthorized";
            }
            else
            {
                result["$.message"] = "*";
            }

            return result;
        }

        if (constraint?.RuleType == "authorization")
        {
            result["$.token"] = "*";
        }
        else if (lower.Contains("register") || lower.Contains("created") || lower.Contains("success"))
        {
            result["$.message"] = "*";
        }

        return result;
    }


    private static string InferFieldName(string text)
    {
        var lower = text?.ToLowerInvariant() ?? string.Empty;
        foreach (var field in new[] { "email", "password", "username", "name", "price", "stock", "categoryid", "category", "token" })
        {
            if (lower.Contains(field, StringComparison.OrdinalIgnoreCase))
            {
                return field == "categoryid" ? "categoryId" : field;
            }
        }

        return "request";
    }

    private static string InferRuleType(string text)
    {
        var lower = text?.ToLowerInvariant() ?? string.Empty;
        if (lower.Contains("duplicate") || lower.Contains("already exists") || lower.Contains("unique"))
        {
            return "uniqueness";
        }

        if (lower.Contains("unauthorized") || lower.Contains("forbidden") || lower.Contains("authorization") || lower.Contains("token"))
        {
            return "authorization";
        }

        if (lower.Contains("required") || lower.Contains("missing") || lower.Contains("bắt buộc"))
        {
            return "required";
        }

        if (lower.Contains("format") || lower.Contains("invalid") || lower.Contains("lowercase") || lower.Contains("email"))
        {
            return "format";
        }

        if (lower.Contains("minimum") || lower.Contains("maximum") || lower.Contains("minlength") || lower.Contains("maxlength") || lower.Contains("tối thiểu") || lower.Contains("tối đa"))
        {
            return "boundary";
        }

        return "behavior";
    }

    private static List<int> GetDefaultStatuses(TestType testType, string httpMethod)
    {
        var method = string.IsNullOrWhiteSpace(httpMethod) ? "GET" : httpMethod.Trim().ToUpperInvariant();
        if (testType == TestType.HappyPath)
        {
            return method switch
            {
                "POST" => new List<int> { 201, 200 },
                "PUT" => new List<int> { 200, 204 },
                "PATCH" => new List<int> { 200, 204 },
                "DELETE" => new List<int> { 204, 200, 202 },
                _ => new List<int> { 200 },
            };
        }

        return new List<int> { 400, 401, 403, 404, 409, 415, 422 };
    }

    private sealed class SrsConstraintCandidate
    {
        public string Constraint { get; init; }

        public string ExpectedOutcome { get; init; }

        public string Field { get; init; }

        public string RuleType { get; init; }
    }
}
