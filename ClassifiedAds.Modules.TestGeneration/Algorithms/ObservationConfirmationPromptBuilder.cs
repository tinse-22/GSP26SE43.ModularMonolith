using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ClassifiedAds.Modules.TestGeneration.Algorithms;

/// <summary>
/// Builds structured prompts using the Observation-Confirmation pattern.
/// Source: COmbine/RBCTest paper (arXiv:2504.17287) Section 3.
///
/// The pattern works in two phases:
/// 1. Observation: Ask LLM to list ALL constraints from the API spec without judgment.
/// 2. Confirmation: Ask LLM to confirm each constraint with evidence.
///
/// For single-shot models, a combined prompt with Chain-of-Thought is also generated.
///
/// Key features from the paper:
/// - Semantic verifier: cross-check constraints against examples in OAS.
/// - Structured output: each constraint maps to a testable assertion.
/// - No hallucinated constraints: only spec-backed constraints are used.
/// </summary>
public class ObservationConfirmationPromptBuilder : IObservationConfirmationPromptBuilder
{
    private const string SystemPromptTemplate = @"You are a precise API test engineer. Your task is to generate test expectations for API endpoints based on TWO sources:
1. The OpenAPI specification provided (primary source).
2. User-provided business rules (supplementary source, if any).

RULES:
- For spec-based constraints: only generate constraints DIRECTLY supported by the specification text.
- For business-rule constraints: generate constraints based on user-provided rules. Mark these with source 'business_rule'.
- Do NOT infer or assume constraints not stated in the spec or business rules.
- Each constraint MUST reference the specific source that supports it.
- Output constraints as structured JSON objects.
- If examples are provided in the spec, cross-check your constraints against them.

OUTPUT FORMAT (JSON array):
[
  {
    ""field"": ""response.body.id"",
    ""constraint"": ""must be a non-empty string"",
    ""type"": ""type_check|value_check|presence_check|format_check|range_check|relationship_check|business_rule_check"",
    ""source"": ""spec|business_rule"",
    ""evidence"": ""Schema defines id as { type: string, format: uuid }"",
    ""confidence"": ""high|medium"",
    ""assertion"": ""expect(response.body.id).toBeType('string')""
  }
]";

    /// <inheritdoc />
    public ObservationConfirmationPrompt BuildForEndpoint(EndpointPromptContext context)
    {
        if (context == null)
        {
            return null;
        }

        var specBlock = BuildSpecBlock(context);

        var observationPrompt = BuildObservationPrompt(context, specBlock);
        var confirmationTemplate = BuildConfirmationPromptTemplate(context);
        var combinedPrompt = BuildCombinedPrompt(context, specBlock);

        return new ObservationConfirmationPrompt
        {
            ObservationPrompt = observationPrompt,
            ConfirmationPromptTemplate = confirmationTemplate,
            CombinedPrompt = combinedPrompt,
            SystemPrompt = SystemPromptTemplate,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<ObservationConfirmationPrompt> BuildForSequence(
        IReadOnlyList<EndpointPromptContext> orderedEndpoints)
    {
        if (orderedEndpoints == null || orderedEndpoints.Count == 0)
        {
            return Array.Empty<ObservationConfirmationPrompt>();
        }

        var results = new List<ObservationConfirmationPrompt>(orderedEndpoints.Count);
        var previousEndpoints = new List<EndpointPromptContext>();

        foreach (var endpoint in orderedEndpoints)
        {
            var prompt = BuildForEndpoint(endpoint);
            if (prompt == null)
            {
                continue;
            }

            // Add cross-endpoint context for dependent endpoints.
            if (previousEndpoints.Count > 0)
            {
                var crossContext = BuildCrossEndpointContext(previousEndpoints, endpoint);
                if (!string.IsNullOrWhiteSpace(crossContext))
                {
                    prompt.ObservationPrompt = prompt.ObservationPrompt + "\n\n" + crossContext;
                    prompt.CombinedPrompt = prompt.CombinedPrompt + "\n\n" + crossContext;
                }
            }

            results.Add(prompt);
            previousEndpoints.Add(endpoint);
        }

        return results;
    }

    /// <summary>
    /// Build the spec block that describes the endpoint for the LLM.
    /// </summary>
    private static string BuildSpecBlock(EndpointPromptContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## API Endpoint Specification");
        sb.AppendLine();
        sb.AppendLine($"**Method:** {context.HttpMethod}");
        sb.AppendLine($"**Path:** {context.Path}");

        if (!string.IsNullOrWhiteSpace(context.OperationId))
        {
            sb.AppendLine($"**OperationId:** {context.OperationId}");
        }

        if (!string.IsNullOrWhiteSpace(context.Summary))
        {
            sb.AppendLine($"**Summary:** {context.Summary}");
        }

        if (!string.IsNullOrWhiteSpace(context.Description))
        {
            sb.AppendLine($"**Description:** {context.Description}");
        }

        // Parameters.
        if (context.Parameters?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Parameters");
            foreach (var param in context.Parameters)
            {
                sb.AppendLine($"- **{param.Name}** (in: {param.In}, required: {param.Required})");
                if (!string.IsNullOrWhiteSpace(param.Description))
                {
                    sb.AppendLine($"  Description: {param.Description}");
                }

                if (!string.IsNullOrWhiteSpace(param.Schema))
                {
                    sb.AppendLine($"  Schema: `{TruncateSchema(param.Schema, 200)}`");
                }
            }
        }

        // Request body.
        if (!string.IsNullOrWhiteSpace(context.RequestBodySchema))
        {
            sb.AppendLine();
            sb.AppendLine("### Request Body Schema");
            sb.AppendLine("```json");
            sb.AppendLine(TruncateSchema(context.RequestBodySchema, 1000));
            sb.AppendLine("```");
        }

        // Responses.
        if (context.Responses?.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Responses");
            foreach (var response in context.Responses)
            {
                sb.AppendLine($"- **{response.StatusCode}**: {response.Description}");
                if (!string.IsNullOrWhiteSpace(response.Schema))
                {
                    sb.AppendLine($"  Schema: `{TruncateSchema(response.Schema, 300)}`");
                }
            }
        }

        // Response body schema.
        if (!string.IsNullOrWhiteSpace(context.ResponseBodySchema))
        {
            sb.AppendLine();
            sb.AppendLine("### Primary Response Body Schema");
            sb.AppendLine("```json");
            sb.AppendLine(TruncateSchema(context.ResponseBodySchema, 1000));
            sb.AppendLine("```");
        }

        // Examples.
        if (!string.IsNullOrWhiteSpace(context.RequestExample))
        {
            sb.AppendLine();
            sb.AppendLine("### Request Example (from spec)");
            sb.AppendLine("```json");
            sb.AppendLine(TruncateSchema(context.RequestExample, 500));
            sb.AppendLine("```");
        }

        if (!string.IsNullOrWhiteSpace(context.ResponseExample))
        {
            sb.AppendLine();
            sb.AppendLine("### Response Example (from spec)");
            sb.AppendLine("```json");
            sb.AppendLine(TruncateSchema(context.ResponseExample, 500));
            sb.AppendLine("```");
        }

        // User-provided business rules.
        if (!string.IsNullOrWhiteSpace(context.BusinessContext))
        {
            sb.AppendLine();
            sb.AppendLine("### User-Provided Business Rules");
            sb.AppendLine();
            sb.AppendLine("The user has provided the following domain-specific business rules for this endpoint.");
            sb.AppendLine("These rules describe constraints NOT captured in the OpenAPI specification.");
            sb.AppendLine("Generate additional test expectations based on these rules (mark source as 'business_rule').");
            sb.AppendLine();
            sb.AppendLine($"> {context.BusinessContext}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Phase 1: Observation prompt.
    /// Ask LLM to list ALL constraints without filtering.
    /// </summary>
    private static string BuildObservationPrompt(EndpointPromptContext context, string specBlock)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Phase 1: Observation");
        sb.AppendLine();
        sb.AppendLine("Analyze the following API endpoint specification and list ALL testable constraints you observe.");
        sb.AppendLine("For each constraint, note:");
        sb.AppendLine("1. Which field/property it applies to");
        sb.AppendLine("2. What the constraint is (type, format, required, range, etc.)");
        sb.AppendLine("3. Where in the spec you found it");
        sb.AppendLine();
        sb.AppendLine("Focus on these categories:");
        sb.AppendLine("- **Type checks**: field types (string, number, boolean, array, object)");
        sb.AppendLine("- **Format checks**: specific formats (uuid, date-time, email, uri)");
        sb.AppendLine("- **Presence checks**: required fields that must be in the response");
        sb.AppendLine("- **Value checks**: specific values, enums, patterns");
        sb.AppendLine("- **Range checks**: min/max for numbers, minLength/maxLength for strings");
        sb.AppendLine("- **Relationship checks**: relationships between fields (e.g., createdAt <= updatedAt)");
        sb.AppendLine("- **Business rule checks**: constraints from user-provided business rules (if any)");
        sb.AppendLine();
        sb.AppendLine("List ALL constraints, even obvious ones. Do NOT filter yet.");
        sb.AppendLine();
        sb.AppendLine(specBlock);

        return sb.ToString();
    }

    /// <summary>
    /// Phase 2: Confirmation prompt template.
    /// To be filled with Phase 1 results. Asks LLM to confirm each constraint.
    /// </summary>
    private static string BuildConfirmationPromptTemplate(EndpointPromptContext context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Phase 2: Confirmation");
        sb.AppendLine();
        sb.AppendLine("You previously identified the following constraints for `{METHOD} {PATH}`:");
        sb.AppendLine();
        sb.AppendLine("{OBSERVATION_RESULTS}");
        sb.AppendLine();
        sb.AppendLine("Now CONFIRM each constraint:");
        sb.AppendLine();
        sb.AppendLine("For each constraint:");
        sb.AppendLine("1. **Find evidence**: Quote the EXACT text from the spec that supports this constraint.");
        sb.AppendLine("2. **Cross-check examples**: If the spec includes examples, verify the constraint is consistent with them.");
        sb.AppendLine("3. **Verify applicability**: Is this constraint for a happy-path (2xx) test case?");
        sb.AppendLine("4. **Decision**: KEEP or REMOVE. Remove if:");
        sb.AppendLine("   - No direct evidence in the spec or business rules (you inferred it)");
        sb.AppendLine("   - Contradicted by examples");
        sb.AppendLine("   - Only applies to error cases (4xx/5xx)");
        sb.AppendLine("   - Too implementation-specific (e.g., response time)");
        sb.AppendLine();
        sb.AppendLine("Output ONLY confirmed constraints in the JSON format specified in the system prompt.");
        sb.AppendLine("Set confidence to 'high' if evidence is explicit, 'medium' if based on schema type inference.");

        return sb.ToString()
            .Replace("{METHOD}", context?.HttpMethod ?? "")
            .Replace("{PATH}", context?.Path ?? "");
    }

    /// <summary>
    /// Combined single-shot prompt with Chain-of-Thought.
    /// For models that don't support multi-turn conversation.
    /// </summary>
    private static string BuildCombinedPrompt(EndpointPromptContext context, string specBlock)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Generate Test Expectations for API Endpoint");
        sb.AppendLine();
        sb.AppendLine("Follow this two-step process to generate ACCURATE test expectations:");
        sb.AppendLine();
        sb.AppendLine("## Step 1: Observe");
        sb.AppendLine("Read the API specification below carefully. List ALL constraints you can find about the response.");
        sb.AppendLine("Look for: types, formats, required fields, enums, ranges, patterns, relationships.");
        sb.AppendLine("Also check for any user-provided business rules listed in the spec block below.");
        sb.AppendLine();
        sb.AppendLine("## Step 2: Confirm");
        sb.AppendLine("For EACH constraint from Step 1:");
        sb.AppendLine("- Find the EXACT text in the spec that supports it.");
        sb.AppendLine("- If examples exist, verify consistency.");
        sb.AppendLine("- REMOVE any constraint you cannot directly back with spec evidence.");
        sb.AppendLine("- Only keep constraints relevant to happy-path (2xx) testing.");
        sb.AppendLine();
        sb.AppendLine("## Step 3: Output");
        sb.AppendLine("Output ONLY confirmed constraints as a JSON array (see system prompt for format).");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(specBlock);

        return sb.ToString();
    }

    /// <summary>
    /// Build cross-endpoint context for dependent endpoints.
    /// Tells the LLM about data flow from previous endpoints.
    /// </summary>
    private static string BuildCrossEndpointContext(
        IReadOnlyList<EndpointPromptContext> previousEndpoints,
        EndpointPromptContext currentEndpoint)
    {
        if (previousEndpoints == null || previousEndpoints.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        sb.AppendLine("## Cross-Endpoint Context");
        sb.AppendLine();
        sb.AppendLine("This endpoint is tested AFTER the following endpoints (in order):");

        var relevantPrevious = previousEndpoints
            .Where(p => IsLikelyRelated(p, currentEndpoint))
            .ToList();

        if (relevantPrevious.Count == 0)
        {
            return null;
        }

        foreach (var prev in relevantPrevious)
        {
            sb.AppendLine($"- `{prev.HttpMethod} {prev.Path}` ({prev.Summary ?? prev.OperationId ?? "no description"})");
        }

        sb.AppendLine();
        sb.AppendLine("Consider data produced by previous endpoints when generating expectations.");
        sb.AppendLine("For example, if POST /users was called before GET /users/{id}, the response should match the created user.");

        return sb.ToString();
    }

    /// <summary>
    /// Check if two endpoints are likely related (share resource path or tokens).
    /// </summary>
    private static bool IsLikelyRelated(EndpointPromptContext a, EndpointPromptContext b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        var aSegments = ExtractPathSegments(a.Path);
        var bSegments = ExtractPathSegments(b.Path);

        // Share at least one non-trivial path segment.
        return aSegments.Intersect(bSegments, StringComparer.OrdinalIgnoreCase).Any();
    }

    /// <summary>
    /// Extract meaningful path segments (skip version prefixes and parameters).
    /// </summary>
    private static HashSet<string> ExtractPathSegments(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return path
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => !s.Contains('{') && s.Length > 2 && !IsVersionPrefix(s))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsVersionPrefix(string segment)
    {
        return segment.StartsWith('v') && segment.Length <= 3 && char.IsDigit(segment[1]);
    }

    private static string TruncateSchema(string schema, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(schema) || schema.Length <= maxLength)
        {
            return schema;
        }

        return schema[..maxLength] + "... (truncated)";
    }
}
