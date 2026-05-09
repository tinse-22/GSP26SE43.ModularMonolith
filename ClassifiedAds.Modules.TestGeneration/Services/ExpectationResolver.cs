using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.Extensions.Logging;
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

    private readonly ILogger<ExpectationResolver> _logger;

    public ExpectationResolver(ILogger<ExpectationResolver> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ResolvedExpectation Resolve(GeneratedScenarioContext context)
    {
        // LLM already received the full SRS document and endpoint spec — it is the most
        // context-aware source for assertions and status codes.
        // Priority: LLM (when it provides valid data) → SRS (for traceability + filling gaps)
        //           → Swagger (schema-based enrichment) → hardcoded defaults.
        //
        // SRS and Swagger are used to:
        //   (a) Supply SRS traceability (PrimaryRequirementId, RequirementCode)
        //   (b) Fill bodyContains / jsonPathChecks when LLM left them empty
        //   (c) Supply status codes ONLY when LLM did not provide any

        var llm = TryResolveFromLlm(context);
        if (llm != null && llm.ExpectedStatusCodes?.Count > 0)
        {
            // LLM has explicit, valid status codes — use them as authoritative.
            // Enrich with SRS traceability and any missing body/jsonPath assertions.
            return EnrichFromSrsAndSwagger(llm, context);
        }

        // LLM didn't provide usable status codes (or provided nothing at all).
        // Fall back to SRS → Swagger → LLM body-only → Default.
        var endpointInfo = !string.IsNullOrWhiteSpace(context.HttpMethod)
            ? $"{context.HttpMethod} (EndpointId={context.EndpointId})"
            : $"EndpointId={context.EndpointId}";

        var (srs, srsReason) = TryResolveFromSrs(context);
        if (srs != null)
        {
            _logger.LogInformation(
                "[ExpectationResolver] ✅ SRS → {Endpoint} | TestType={TestType} | ReqCode={ReqCode} | ExpectedStatus=[{Statuses}] | BodyContains=[{BodyContains}] | JsonPathChecks=[{JsonPathChecks}]",
                endpointInfo,
                context.TestType,
                srs.RequirementCode,
                string.Join(",", srs.ExpectedStatusCodes ?? new List<int>()),
                string.Join(",", srs.BodyContains ?? new List<string>()),
                string.Join(", ", (srs.JsonPathChecks ?? new Dictionary<string, string>()).Select(kv => $"{kv.Key}:{kv.Value}")));
            return srs;
        }

        // Log WHY SRS wasn't used (only if SRS data exists but didn't match)
        if (!string.IsNullOrWhiteSpace(srsReason))
        {
            _logger.LogWarning(
                "[ExpectationResolver] ⚠️ SRS SKIPPED → {Endpoint} | TestType={TestType} | Reason: {Reason}",
                endpointInfo,
                context.TestType,
                srsReason);
        }

        var swagger = TryResolveFromSwagger(context);
        if (swagger != null)
        {
            _logger.LogInformation(
                "[ExpectationResolver] 🔄 Swagger → {Endpoint} | TestType={TestType} | ExpectedStatus=[{Statuses}]",
                endpointInfo,
                context.TestType,
                string.Join(",", swagger.ExpectedStatusCodes ?? new List<int>()));
            return swagger;
        }

        // LLM had some assertions but no valid status — return with default status.
        if (llm != null)
        {
            _logger.LogInformation(
                "[ExpectationResolver] 🤖 LLM → {Endpoint} | TestType={TestType} | ExpectedStatus=[{Statuses}]",
                endpointInfo,
                context.TestType,
                string.Join(",", llm.ExpectedStatusCodes ?? new List<int>()));
            return llm;
        }

        var def = BuildDefault(context);
        _logger.LogWarning(
            "[ExpectationResolver] ❌ DEFAULT (no SRS/Swagger/LLM) → {Endpoint} | TestType={TestType} | HttpMethod={HttpMethod} | FallbackStatus=[{Statuses}] | ⚠️ Missing SRS constraints or Swagger errorResponses!",
            endpointInfo,
            context.TestType,
            context.HttpMethod,
            string.Join(",", def.ExpectedStatusCodes ?? new List<int>()));
        return def;
    }

    /// <summary>
    /// Takes an LLM-resolved expectation as the authoritative base and supplements it
    /// with SRS traceability and any assertions that the LLM left empty.
    /// The LLM's status codes, bodyContains, and jsonPathChecks are NEVER overridden.
    /// </summary>
    private static ResolvedExpectation EnrichFromSrsAndSwagger(
        ResolvedExpectation llm,
        GeneratedScenarioContext context)
    {
        var (srs, _) = TryResolveFromSrs(context);
        var swagger = llm.ResponseSchema == null ? TryResolveFromSwagger(context) : null;

        // Supplement bodyContains only when LLM provided none.
        var bodyContains = llm.BodyContains?.Count > 0
            ? llm.BodyContains
            : srs?.BodyContains?.Count > 0
                ? srs.BodyContains
                : swagger?.BodyContains ?? new List<string>();

        // Supplement jsonPathChecks only when LLM provided none.
        var jsonPathChecks = llm.JsonPathChecks?.Count > 0
            ? llm.JsonPathChecks
            : srs?.JsonPathChecks?.Count > 0
                ? srs.JsonPathChecks
                : swagger?.JsonPathChecks ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Supplement bodyNotContains only when LLM provided none.
        var bodyNotContains = llm.BodyNotContains?.Count > 0
            ? llm.BodyNotContains
            : srs?.BodyNotContains ?? new List<string>();

        return new ResolvedExpectation
        {
            ExpectedStatusCodes = llm.ExpectedStatusCodes,
            ResponseSchema = llm.ResponseSchema ?? swagger?.ResponseSchema,
            HeaderChecks = llm.HeaderChecks,
            BodyContains = bodyContains,
            BodyNotContains = bodyNotContains,
            JsonPathChecks = jsonPathChecks,
            MaxResponseTime = llm.MaxResponseTime,
            Source = ExpectationSource.Llm,
            // Preserve SRS traceability even when LLM drives the assertions.
            PrimaryRequirementId = llm.PrimaryRequirementId ?? srs?.PrimaryRequirementId,
            RequirementCode = llm.RequirementCode ?? srs?.RequirementCode,
        };
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

        var expectedStatus = resolved.ExpectedStatusCodes?.Count > 0 ? resolved.ExpectedStatusCodes : new List<int> { 200 };

        return new N8nTestCaseExpectation
        {
            ExpectedStatus = expectedStatus,
            ResponseSchema = resolved.ResponseSchema,
            HeaderChecks = resolved.HeaderChecks ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BodyContains = resolved.BodyContains ?? new List<string>(),
            BodyNotContains = resolved.BodyNotContains ?? new List<string>(),
            JsonPathChecks = ReconcileJsonPathChecksWithStatuses(resolved.JsonPathChecks, expectedStatus),
            MaxResponseTime = resolved.MaxResponseTime,
            ExpectationSource = resolved.Source.ToString(),
            RequirementCode = resolved.RequirementCode,
            PrimaryRequirementId = resolved.PrimaryRequirementId,
        };
    }

    internal static Dictionary<string, string> ReconcileJsonPathChecksWithStatuses(
        Dictionary<string, string> jsonPathChecks,
        IReadOnlyCollection<int> expectedStatuses)
    {
        var result = jsonPathChecks?.Count > 0
            ? new Dictionary<string, string>(jsonPathChecks, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (result.Count == 0 || expectedStatuses == null || expectedStatuses.Count == 0)
        {
            return result;
        }

        var hasSuccessStatus = expectedStatuses.Any(code => code >= 200 && code <= 299);
        var hasFailureStatus = expectedStatuses.Any(code => code < 200 || code >= 300);
        if (hasSuccessStatus == hasFailureStatus)
        {
            return result;
        }

        foreach (var key in result.Keys.ToList())
        {
            if (!IsSuccessJsonPath(key))
            {
                continue;
            }

            result[key] = hasSuccessStatus ? "true" : "false";
        }

        return result;
    }

    private static bool IsSuccessJsonPath(string jsonPath)
    {
        if (string.IsNullOrWhiteSpace(jsonPath))
        {
            return false;
        }

        var normalized = jsonPath.Trim();
        return normalized.Equals("$.success", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".success", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("['success']", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("[\"success\"]", StringComparison.OrdinalIgnoreCase);
    }

    private static (ResolvedExpectation Result, string SkipReason) TryResolveFromSrs(GeneratedScenarioContext context)
    {
        var requirements = RankRequirements(context).ToList();

        // Phase 1: Try pre-parsed JSON testableConstraints first
        foreach (var requirement in requirements)
        {
            var constraints = ParseConstraints(requirement);
            if (constraints.Count == 0)
            {
                continue;
            }

            foreach (var constraint in constraints)
            {
                // When TargetFieldName is specified, only apply constraints that target
                // the same field (or have no specific field, i.e. whole-body constraints).
                // This prevents a generic "email format → 400" constraint from being
                // applied to every body mutation (empty body, missing password, etc.).
                if (!string.IsNullOrWhiteSpace(context.TargetFieldName)
                    && !string.IsNullOrWhiteSpace(constraint.Field)
                    && !string.Equals(constraint.Field, context.TargetFieldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // For read-only HTTP methods (GET/HEAD/OPTIONS), skip constraints whose
                // field is a typical request-body field (email, password, username, etc.).
                // GET endpoints have no request body, so body-field constraints from SRS
                // should not override path-parameter mutation expectations.
                if (IsReadOnlyHttpMethod(context.HttpMethod)
                    && !string.IsNullOrWhiteSpace(constraint.Field)
                    && IsBodyOnlyField(constraint.Field))
                {
                    continue;
                }

                var statuses = NormalizeStatuses(ParseStatusCodes(constraint.ExpectedOutcome, constraint.Constraint), context.TestType);
                if (statuses.Count == 0)
                {
                    continue;
                }

                var bodyContains = ExtractBodyContains(requirement, constraint, context.TestType);
                var bodyNotContains = ExtractBodyNotContains(requirement, constraint.Constraint);
                var srsJsonPathChecks = BuildSrsJsonPathChecks(requirement, constraint, context.TestType, statuses);

                // If SRS could not derive any jsonPath assertions, fall back to what the LLM already
                // suggested — the LLM had the full SRS context and produced API-specific assertions.
                // This prevents overriding LLM's contextual checks with empty/generic ones.
                var jsonPathChecks = srsJsonPathChecks.Count > 0
                    ? srsJsonPathChecks
                    : (context.LlmExpectation?.JsonPathChecks?.Count > 0
                        ? new Dictionary<string, string>(context.LlmExpectation.JsonPathChecks, StringComparer.OrdinalIgnoreCase)
                        : srsJsonPathChecks);

                // If SRS bodyContains is empty, fall back to LLM's bodyContains.
                var finalBodyContains = bodyContains.Count > 0
                    ? bodyContains
                    : (context.LlmExpectation?.BodyContains?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList() ?? bodyContains);

                return (new ResolvedExpectation
                {
                    ExpectedStatusCodes = statuses,
                    BodyContains = finalBodyContains,
                    BodyNotContains = bodyNotContains,
                    JsonPathChecks = jsonPathChecks,
                    Source = ExpectationSource.Srs,
                    PrimaryRequirementId = requirement.Id,
                    RequirementCode = requirement.RequirementCode,
                }, null);
            }
        }

        // Phase 2: Fallback — parse SRS markdown content directly when JSON constraints are missing
        var srsContent = context?.SrsDocumentContent;
        if (!string.IsNullOrWhiteSpace(srsContent))
        {
            var markdownConstraints = ParseConstraintsFromMarkdown(srsContent, context);
            foreach (var mc in markdownConstraints)
            {
                var statuses = NormalizeStatuses(ParseStatusCodes(mc.ExpectedOutcome, mc.Constraint), context.TestType);
                if (statuses.Count == 0)
                {
                    continue;
                }

                var bodyContains = ExtractBodyContainsFromMarkdown(mc, context.TestType);
                var jsonPathChecks = BuildSrsJsonPathChecksFromMarkdown(mc, context.TestType, statuses);

                return (new ResolvedExpectation
                {
                    ExpectedStatusCodes = statuses,
                    BodyContains = bodyContains,
                    JsonPathChecks = jsonPathChecks,
                    Source = ExpectationSource.Srs,
                    RequirementCode = mc.RequirementCode,
                }, null);
            }

            var hasAnySrs = context?.SrsRequirements?.Any() == true;
            return (null, hasAnySrs
                ? $"SRS data exists ({context.SrsRequirements.Count} reqs) but none match this endpoint ({context.EndpointId}) or test type ({context.TestType}). Check: IsReviewed=true, EndpointId match, constraints have status codes."
                : "SRS markdown content was parsed but no constraint matched this endpoint/test type.");
        }

        var hasSrs = context?.SrsRequirements?.Any() == true;
        return (null, hasSrs
            ? $"SRS data exists ({context.SrsRequirements.Count} reqs) but none match this endpoint ({context.EndpointId}) or test type ({context.TestType}). Check: IsReviewed=true, EndpointId match, constraints have status codes."
            : null);
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

        // If LLM provided no status at all, supply a sensible default so the caller can
        // detect "LLM had no status" vs "LLM had some statuses" in Resolve().
        var statuses = normalized.ExpectedStatusCodes?.Count > 0
            ? normalized.ExpectedStatusCodes
            : GetDefaultStatuses(context?.TestType ?? TestType.Negative, context?.HttpMethod);

        return new ResolvedExpectation
        {
            ExpectedStatusCodes = statuses,
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

        // Trust the LLM's status codes as-is — the LLM already read the SRS and endpoint
        // spec, so it knows which codes are appropriate. Only validate the HTTP range.
        // Do NOT filter by TestType here: a Boundary test may legitimately expect 2xx
        // (testing at the valid edge of a constraint), and the LLM knows this.
        var statuses = (expectation.ExpectedStatus ?? new List<int>())
            .Where(code => code >= 100 && code <= 599)
            .Distinct()
            .ToList();
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
        var candidates = context.SrsRequirements
            .Where(x => !x.EndpointId.HasValue || x.EndpointId == context.EndpointId)
            .ToList();

        if (candidates.Count == 0)
        {
            return Enumerable.Empty<SrsRequirement>();
        }

        // Prefer reviewed requirements, but fall back to unreviewed if none are reviewed yet.
        var pool = candidates.Any(x => x.IsReviewed)
            ? candidates.Where(x => x.IsReviewed).ToList()
            : candidates;

        return pool
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

        // Removed: generic Add("error") fallback when result is empty.
        // Returning an empty list lets the caller fall back to LLM's bodyContains,
        // which is API-specific (derived from SRS by the LLM).

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

        // NOTE: Do NOT add $.success or $.message generically here.
        // These fields are API-specific — not all APIs return {success: bool, message: string}.
        // The LLM already received the full SRS and produces API-aware assertions.
        // Only emit JSONPath assertions when we have concrete constraint-specific evidence.

        if (!isSuccess)
        {
            if (constraint?.RuleType == "authorization")
            {
                // Authorization constraints reliably produce a message indicating the reason.
                result["$.message"] = lower.Contains("forbidden") ? "forbidden" : "unauthorized";
            }
            // Removed: result["$.errors.*"] = constraint.Field — assumes {errors: {field: ...}} structure,
            // which is API-specific and wrong for APIs that use {fieldErrors, violations, error, ...}.
            // The LLM's jsonPathChecks (used as fallback in TryResolveFromSrs) handles field-level assertions.

            return result;
        }

        if (constraint?.RuleType == "authorization")
        {
            result["$.token"] = "*";
        }
        // Removed: keyword-matching result["$.message"] = "*" for register/created/success.
        // The LLM's jsonPathChecks (used as fallback in TryResolveFromSrs) will cover this.

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

    /// <summary>
    /// Returns true for HTTP methods that never carry a request body.
    /// </summary>
    private static bool IsReadOnlyHttpMethod(string httpMethod)
    {
        var method = (httpMethod ?? string.Empty).Trim().ToUpperInvariant();
        return method is "GET" or "HEAD" or "OPTIONS";
    }

    /// <summary>
    /// Returns true for field names that only appear in request bodies,
    /// never in path/query parameters.
    /// </summary>
    private static bool IsBodyOnlyField(string field)
    {
        var normalized = (field ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            var f when f.Contains("email") => true,
            var f when f.Contains("password") => true,
            var f when f.Contains("username") => true,
            var f when f.Contains("phone") => true,
            var f when f.Contains("address") => true,
            _ => false,
        };
    }

    private sealed class SrsConstraintCandidate
    {
        public string Constraint { get; init; }

        public string ExpectedOutcome { get; init; }

        public string Field { get; init; }

        public string RuleType { get; init; }
    }

    private sealed class MarkdownConstraintCandidate
    {
        public string Constraint { get; init; }

        public string ExpectedOutcome { get; init; }

        public string Field { get; init; }

        public string RuleType { get; init; }

        public string RequirementCode { get; init; }

        public string EndpointPath { get; init; }

        public string HttpMethod { get; init; }
    }

    /// <summary>
    /// Parses SRS markdown content directly to extract validation rules
    /// when testableConstraints JSON is empty. Recognizes the standard SRS format:
    /// - Ràng Buộc Dữ Liệu / Validation Rules tables
    /// - Test case specifications with Expected Response status codes
    /// - Constraint patterns like "password >= 6 chars → 400"
    /// </summary>
    private static List<MarkdownConstraintCandidate> ParseConstraintsFromMarkdown(
        string srsContent,
        GeneratedScenarioContext context)
    {
        var results = new List<MarkdownConstraintCandidate>();
        if (string.IsNullOrWhiteSpace(srsContent))
        {
            return results;
        }

        var httpMethod = context?.HttpMethod?.Trim().ToUpperInvariant() ?? "";
        var endpointId = context?.EndpointId;
        var testType = context?.TestType ?? TestType.Negative;

        // === Pattern 1: Extract validation rules from Ràng Buộc Dữ Liệu table ===
        // Format: | Entity | Field | Quy tắc |
        // Example: | User | `password` | Tối thiểu 6 ký tự |
        var validationTableRegex = new Regex(
            @"\|\s*(\w+)\s*\|\s*`(\w+)`\s*\|\s*(.+?)\s*\|",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var validationSectionMatch = Regex.Match(srsContent,
            @"(?:Ràng\s*Buộc\s*Dữ\s*Liệu|Validation\s*Rules).*?\n((?:\|.*\|.*\|.*\|\n?)+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (validationSectionMatch.Success)
        {
            var tableBlock = validationSectionMatch.Groups[1].Value;
            foreach (Match m in validationTableRegex.Matches(tableBlock))
            {
                var entity = m.Groups[1].Value.Trim();
                var field = m.Groups[2].Value.Trim().ToLowerInvariant();
                var rule = m.Groups[3].Value.Trim();

                if (entity.Equals("User", StringComparison.OrdinalIgnoreCase)
                    || entity.Equals("Category", StringComparison.OrdinalIgnoreCase)
                    || entity.Equals("Product", StringComparison.OrdinalIgnoreCase))
                {
                    var constraint = MapValidationRuleToConstraint(entity, field, rule,
                        httpMethod, testType);

                    if (constraint != null)
                    {
                        results.Add(constraint);
                    }
                }
            }
        }

        // === Pattern 2: Extract from test case specifications ===
        // Format: TC-AUTH-REG-004: Password quá ngắn (dưới 6 ký tự)
        // Followed by Expected Response (400 Bad Request)
        var testCaseRegex = new Regex(
            @"####\s+(TC-[\w-]+):\s*(.+?)\n.*?\*\*Expected Response.*?(\d{3})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match m in testCaseRegex.Matches(srsContent))
        {
            var tcCode = m.Groups[1].Value.Trim();
            var tcTitle = m.Groups[2].Value.Trim();
            var statusCode = int.TryParse(m.Groups[3].Value, out var code) ? code : 0;

            if (statusCode < 100 || statusCode > 599)
            {
                continue;
            }

            // Extract endpoint info from the test case section
            var tcBlock = GetTestCaseBlock(srsContent, tcCode);
            var tcEndpoint = ExtractEndpointFromBlock(tcBlock);
            var tcMethod = ExtractMethodFromBlock(tcBlock);

            // Match against current context
            if (!string.IsNullOrWhiteSpace(tcEndpoint)
                && !string.IsNullOrWhiteSpace(context?.HttpMethod)
                && !IsEndpointMatch(tcEndpoint, tcMethod, httpMethod))
            {
                continue;
            }

            var (field, ruleType) = InferFromTitle(tcTitle, tcBlock);
            var isSuccess = statusCode >= 200 && statusCode <= 299;
            var matchesTestType = (testType == TestType.HappyPath && isSuccess)
                || (testType != TestType.HappyPath && !isSuccess);

            if (!matchesTestType)
            {
                continue;
            }

            results.Add(new MarkdownConstraintCandidate
            {
                Constraint = tcTitle,
                ExpectedOutcome = statusCode.ToString(),
                Field = field,
                RuleType = ruleType,
                RequirementCode = tcCode,
                EndpointPath = tcEndpoint,
                HttpMethod = tcMethod,
            });
        }

        // === Pattern 3: Extract from Ràng Buộc Dữ Liệu section's inline constraints ===
        // Look for patterns like "password >= 6 chars → 400" in the document
        var inlineConstraintRegex = new Regex(
            @"(?:`(\w+)`|(\w+))\s*(?:phải|must|tối thiểu|minimum|>=|>=|tối đa|maximum|<=|<=|định dạng|format|duy nhất|unique)\s*([^→\n]+?)(?:→|->)\s*(\d{3})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        foreach (Match m in inlineConstraintRegex.Matches(srsContent))
        {
            var field = (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim().ToLowerInvariant();
            var ruleDesc = m.Groups[3].Value.Trim();
            var statusCode = int.TryParse(m.Groups[4].Value, out var code) ? code : 0;

            if (statusCode < 100 || statusCode > 599)
            {
                continue;
            }

            var isSuccess = statusCode >= 200 && statusCode <= 299;
            if ((testType == TestType.HappyPath && !isSuccess)
                || (testType != TestType.HappyPath && isSuccess))
            {
                continue;
            }

            results.Add(new MarkdownConstraintCandidate
            {
                Constraint = $"{field} {ruleDesc}",
                ExpectedOutcome = statusCode.ToString(),
                Field = field,
                RuleType = InferRuleType(ruleDesc),
                RequirementCode = "SRS-MD",
            });
        }

        // === Pattern 4: Extract from HTTP Status Code table ===
        // | Code | Ý nghĩa |
        // | 400 | Dữ liệu đầu vào không hợp lệ |
        var statusTableRegex = new Regex(
            @"\|\s*(\d{3})\s*\|\s*(.+?)\s*\|",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        var statusSectionMatch = Regex.Match(srsContent,
            @"HTTP\s*Status\s*Code.*?\n((?:\|.*\|.*\|\n?)+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        if (statusSectionMatch.Success)
        {
            // This provides general status code meanings but no field-specific constraints
            // We add them as low-priority fallback constraints
        }

        return results;
    }

    /// <summary>
    /// Maps a validation rule from the SRS table to a MarkdownConstraintCandidate.
    /// </summary>
    private static MarkdownConstraintCandidate MapValidationRuleToConstraint(
        string entity, string field, string rule,
        string httpMethod, TestType testType)
    {
        var ruleLower = rule.ToLowerInvariant();
        int? statusCode = null;
        string ruleType = null;
        string constraintText = rule;

        // Password minimum length
        if (field == "password" && (ruleLower.Contains("tối thiểu") || ruleLower.Contains("minimum")
            || ruleLower.Contains("minlength") || ruleLower.Contains("6 ký tự")
            || ruleLower.Contains("6 char")))
        {
            ruleType = "boundary";
            // If generating negative/boundary: too-short password → 400
            // If generating happy path: valid password → 201
            statusCode = testType == TestType.HappyPath ? 201 : 400;
            constraintText = testType == TestType.HappyPath
                ? "password >= 6 chars → 201"
                : "password < 6 chars → 400";
        }
        // Email format
        else if (field == "email" && (ruleLower.Contains("định dạng") || ruleLower.Contains("format")
            || ruleLower.Contains("hợp lệ") || ruleLower.Contains("valid")))
        {
            ruleType = "format";
            statusCode = testType == TestType.HappyPath ? 201 : 400;
            constraintText = testType == TestType.HappyPath
                ? "valid email format → 201"
                : "invalid email format → 400";
        }
        // Email uniqueness
        else if (field == "email" && (ruleLower.Contains("duy nhất") || ruleLower.Contains("unique")))
        {
            ruleType = "uniqueness";
            statusCode = testType == TestType.HappyPath ? 201 : 409;
            constraintText = testType == TestType.HappyPath
                ? "unique email → 201"
                : "duplicate email → 409";
        }
        // Email lowercase
        else if (field == "email" && ruleLower.Contains("lowercase"))
        {
            ruleType = "format";
            statusCode = 201;
            constraintText = "email auto-lowercase → 201";
        }
        // Name minimum length
        else if (field == "name" && (ruleLower.Contains("tối thiểu") || ruleLower.Contains("minimum")
            || ruleLower.Contains("1 ký tự") || ruleLower.Contains("1 char")))
        {
            ruleType = "boundary";
            statusCode = testType == TestType.HappyPath ? 201 : 400;
            constraintText = testType == TestType.HappyPath
                ? "name >= 1 char → 201"
                : "empty name → 400";
        }
        // Price non-negative
        else if (field == "price" && (ruleLower.Contains("không âm") || ruleLower.Contains("non-negative")
            || ruleLower.Contains(">= 0") || ruleLower.Contains(">=0")))
        {
            ruleType = "boundary";
            statusCode = testType == TestType.HappyPath ? 201 : 400;
            constraintText = testType == TestType.HappyPath
                ? "price >= 0 → 201"
                : "negative price → 400";
        }
        // Stock non-negative
        else if (field == "stock" && (ruleLower.Contains("không âm") || ruleLower.Contains("non-negative")
            || ruleLower.Contains(">= 0") || ruleLower.Contains(">=0")))
        {
            ruleType = "boundary";
            statusCode = testType == TestType.HappyPath ? 201 : 400;
            constraintText = testType == TestType.HappyPath
                ? "stock >= 0 → 201"
                : "negative stock → 400";
        }

        if (!statusCode.HasValue || string.IsNullOrWhiteSpace(ruleType))
        {
            return null;
        }

        return new MarkdownConstraintCandidate
        {
            Constraint = constraintText,
            ExpectedOutcome = statusCode.Value.ToString(),
            Field = field,
            RuleType = ruleType,
            RequirementCode = "SRS-TABLE",
        };
    }

    private static string GetTestCaseBlock(string srsContent, string tcCode)
    {
        var startIdx = srsContent.IndexOf(tcCode, StringComparison.OrdinalIgnoreCase);
        if (startIdx < 0) return "";

        // Find the next test case or section header
        var nextTcIdx = srsContent.IndexOf("#### TC-", startIdx + tcCode.Length, StringComparison.OrdinalIgnoreCase);
        if (nextTcIdx < 0)
        {
            nextTcIdx = srsContent.IndexOf("## ", startIdx + tcCode.Length, StringComparison.OrdinalIgnoreCase);
        }

        var endIdx = nextTcIdx > 0 ? nextTcIdx : Math.Min(startIdx + 2000, srsContent.Length);
        return srsContent.Substring(startIdx, Math.Min(endIdx - startIdx, 2000));
    }

    private static string ExtractEndpointFromBlock(string block)
    {
        if (string.IsNullOrWhiteSpace(block)) return "";
        var match = Regex.Match(block, @"\*\*Endpoint:\*\*\s*`?\s*(\w+)\s+(/[^\s`\n]+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[2].Value.Trim() : "";
    }

    private static string ExtractMethodFromBlock(string block)
    {
        if (string.IsNullOrWhiteSpace(block)) return "";
        var match = Regex.Match(block, @"\*\*Endpoint:\*\*\s*`?\s*(\w+)\s+",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim().ToUpperInvariant() : "";
    }

    private static bool IsEndpointMatch(string tcEndpoint, string tcMethod, string contextMethod)
    {
        if (string.IsNullOrWhiteSpace(tcEndpoint) || string.IsNullOrWhiteSpace(contextMethod))
        {
            return true; // Can't match, so don't filter out
        }

        // Match method
        if (!string.IsNullOrWhiteSpace(tcMethod)
            && !tcMethod.Equals(contextMethod, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Match path pattern (e.g., /api/auth/register matches register endpoint)
        var tcPathLower = tcEndpoint.ToLowerInvariant();
        return tcPathLower.Contains("register") || tcPathLower.Contains("login")
            || tcPathLower.Contains("category") || tcPathLower.Contains("product")
            || tcPathLower.Contains("health");
    }

    private static (string Field, string RuleType) InferFromTitle(string title, string block)
    {
        var lower = (title + " " + block).ToLowerInvariant();

        if (lower.Contains("password") && (lower.Contains("ngắn") || lower.Contains("short")
            || lower.Contains("minimum") || lower.Contains("minlength") || lower.Contains("tối thiểu")))
        {
            return ("password", "boundary");
        }
        if (lower.Contains("password") && (lower.Contains("dài") || lower.Contains("long")
            || lower.Contains("maximum") || lower.Contains("tối đa")))
        {
            return ("password", "boundary");
        }
        if (lower.Contains("email") && (lower.Contains("tồn tại") || lower.Contains("already exists")
            || lower.Contains("duplicate") || lower.Contains("đã được đăng ký")))
        {
            return ("email", "uniqueness");
        }
        if (lower.Contains("email") && (lower.Contains("không hợp lệ") || lower.Contains("invalid")
            || lower.Contains("format")))
        {
            return ("email", "format");
        }
        if (lower.Contains("email") && (lower.Contains("hoa") || lower.Contains("upper")
            || lower.Contains("case-insensitive") || lower.Contains("lowercase")))
        {
            return ("email", "format");
        }
        if (lower.Contains("thiếu") || lower.Contains("missing") || lower.Contains("required")
            || lower.Contains("bắt buộc"))
        {
            return ("request", "required");
        }
        if (lower.Contains("sai") || lower.Contains("wrong") || lower.Contains("invalid"))
        {
            return ("request", "behavior");
        }
        if (lower.Contains("không tồn tại") || lower.Contains("not found") || lower.Contains("not exist"))
        {
            return ("request", "notfound");
        }
        if (lower.Contains("đăng nhập") || lower.Contains("login") || lower.Contains("token")
            || lower.Contains("xác thực") || lower.Contains("auth"))
        {
            return ("token", "authorization");
        }
        if (lower.Contains("biên") || lower.Contains("boundary"))
        {
            return ("request", "boundary");
        }

        return ("request", "behavior");
    }

    private static List<string> ExtractBodyContainsFromMarkdown(
        MarkdownConstraintCandidate mc,
        TestType testType)
    {
        var result = new List<string>();
        var lower = (mc.Constraint + " " + mc.Field + " " + mc.RuleType).ToLowerInvariant();

        void Add(string value)
        {
            if (!string.IsNullOrWhiteSpace(value)
                && !result.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(value);
            }
        }

        if (!string.IsNullOrWhiteSpace(mc.Field) && !mc.Field.Equals("request", StringComparison.OrdinalIgnoreCase))
        {
            Add(mc.Field);
        }

        if (mc.RuleType == "required") Add("required");
        if (mc.RuleType == "format") Add("invalid");
        if (mc.RuleType == "uniqueness") Add("already exists");
        if (mc.RuleType == "authorization") Add("unauthorized");
        if (mc.RuleType == "notfound") Add("not found");
        if (mc.RuleType == "boundary")
        {
            if (lower.Contains("minimum") || lower.Contains("tối thiểu") || lower.Contains("ngắn"))
                Add("minimum");
            if (lower.Contains("maximum") || lower.Contains("tối đa") || lower.Contains("dài"))
                Add("maximum");
        }

        if (lower.Contains("validation failed")) Add("Validation failed");
        if (lower.Contains("registered successfully")) Add("registered successfully");
        if (lower.Contains("login successful")) Add("Login successful");
        if (lower.Contains("not found")) Add("not found");
        if (lower.Contains("conflict")) Add("conflict");
        if (lower.Contains("lowercase")) Add("lowercase");

        if (result.Count == 0 && testType != TestType.HappyPath)
        {
            Add("error");
        }

        return result.Take(3).ToList();
    }

    private static Dictionary<string, string> BuildSrsJsonPathChecksFromMarkdown(
        MarkdownConstraintCandidate mc,
        TestType testType,
        IReadOnlyList<int> statuses)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var isSuccess = statuses != null && statuses.Any(code => code is >= 200 and < 300);

        result["$.success"] = isSuccess ? "true" : "false";

        if (!isSuccess)
        {
            if (!string.IsNullOrWhiteSpace(mc.Field) && !mc.Field.Equals("request", StringComparison.OrdinalIgnoreCase))
            {
                result["$.errors.*"] = mc.Field;
            }
            else if (mc.RuleType == "authorization")
            {
                result["$.message"] = "unauthorized";
            }
            else
            {
                result["$.message"] = "*";
            }
        }
        else if (mc.RuleType == "authorization")
        {
            result["$.token"] = "*";
        }

        return result;
    }
}
