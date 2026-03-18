using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Modules.LlmAssistant.ConfigurationOptions;
using ClassifiedAds.Modules.LlmAssistant.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace ClassifiedAds.Modules.LlmAssistant.Services;

public class FailureExplanationPromptBuilder : IFailureExplanationPromptBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new ()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly FailureExplanationOptions _options;

    public FailureExplanationPromptBuilder(IOptions<LlmAssistantModuleOptions> options)
    {
        _options = options?.Value?.FailureExplanation ?? new FailureExplanationOptions();
    }

    public FailureExplanationPrompt Build(
        TestFailureExplanationContextDto context,
        ApiEndpointMetadataDto endpointMetadata)
    {
        return new FailureExplanationPrompt
        {
            Provider = _options.Provider,
            Model = _options.Model,
            Prompt = BuildPrompt(context, endpointMetadata),
            SanitizedContextJson = JsonSerializer.Serialize(new
            {
                context,
                endpointMetadata,
            }, JsonOptions),
            SanitizedContext = context,
            EndpointMetadata = endpointMetadata,
        };
    }

    private static string BuildPrompt(
        TestFailureExplanationContextDto context,
        ApiEndpointMetadataDto endpointMetadata)
    {
        var builder = new StringBuilder();

        builder.AppendLine("Ban la tro ly giai thich nguyen nhan that bai cua test API.");
        builder.AppendLine("Khong duoc quyet dinh pass/fail. Chi giai thich cac nguyen nhan co kha nang xay ra.");
        builder.AppendLine("Chi duoc tra ve JSON hop le, khong them markdown, khong them giai thich ben ngoai.");
        builder.AppendLine("JSON bat buoc dung schema sau:");
        builder.AppendLine("{");
        builder.AppendLine("  \"summaryVi\": \"string\",");
        builder.AppendLine("  \"possibleCauses\": [\"string\"],");
        builder.AppendLine("  \"suggestedNextActions\": [\"string\"],");
        builder.AppendLine("  \"confidence\": \"Low|Medium|High\",");
        builder.AppendLine("  \"model\": \"string\",");
        builder.AppendLine("  \"tokensUsed\": 0");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("Failure reasons deterministic:");
        builder.AppendLine(JsonSerializer.Serialize(new
        {
            suiteId = context.TestSuiteId,
            runId = context.TestRunId,
            testCaseId = context.Definition?.TestCaseId,
            runNumber = context.RunNumber,
            resolvedEnvironmentName = context.ResolvedEnvironmentName,
            failureReasons = context.ActualResult?.FailureReasons,
        }, JsonOptions));
        builder.AppendLine();
        builder.AppendLine("Original test definition:");
        builder.AppendLine(JsonSerializer.Serialize(context.Definition, JsonOptions));
        builder.AppendLine();
        builder.AppendLine("Actual response and execution result:");
        builder.AppendLine(JsonSerializer.Serialize(context.ActualResult, JsonOptions));

        if (endpointMetadata != null)
        {
            builder.AppendLine();
            builder.AppendLine("Endpoint metadata:");
            builder.AppendLine(JsonSerializer.Serialize(endpointMetadata, JsonOptions));
        }

        return builder.ToString();
    }
}
