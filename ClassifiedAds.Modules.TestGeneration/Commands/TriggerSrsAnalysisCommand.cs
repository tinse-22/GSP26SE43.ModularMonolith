using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Storage.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class TriggerSrsAnalysisCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid CurrentUserId { get; set; }

    /// <summary>Output: the created job ID.</summary>
    public Guid JobId { get; set; }
}

public class TriggerSrsAnalysisCommandHandler : ICommandHandler<TriggerSrsAnalysisCommand>
{
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<SrsAnalysisJob, Guid> _jobRepository;
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly IN8nIntegrationService _n8nService;
    private readonly N8nIntegrationOptions _n8nOptions;
    private readonly IStorageFileGatewayService _storageFileGateway;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger<TriggerSrsAnalysisCommandHandler> _logger;

    private const string WebhookName = "analyze-srs";

    private static readonly Regex StatusCodeRegex = new(@"\b([1-5]\d\d)\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MinLengthRegex = new(@"(?:min(?:imum)?\s*length|min\s*length|at\s*least|>=?)\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MaxLengthRegex = new(@"(?:max(?:imum)?\s*length|at\s*most|<=?)\s*(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public TriggerSrsAnalysisCommandHandler(
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<SrsAnalysisJob, Guid> jobRepository,
        IRepository<TestSuite, Guid> suiteRepository,
        IApiEndpointMetadataService endpointMetadataService,
        IN8nIntegrationService n8nService,
        IOptions<N8nIntegrationOptions> n8nOptions,
        IStorageFileGatewayService storageFileGateway,
        Dispatcher dispatcher,
        ILogger<TriggerSrsAnalysisCommandHandler> logger)
    {
        _srsDocumentRepository = srsDocumentRepository;
        _jobRepository = jobRepository;
        _suiteRepository = suiteRepository;
        _endpointMetadataService = endpointMetadataService;
        _n8nService = n8nService;
        _n8nOptions = n8nOptions?.Value ?? new N8nIntegrationOptions();
        _storageFileGateway = storageFileGateway;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task HandleAsync(TriggerSrsAnalysisCommand command, CancellationToken cancellationToken = default)
    {
        var doc = await _srsDocumentRepository.FirstOrDefaultAsync(
            _srsDocumentRepository.GetQueryableSet()
                .Where(x => x.Id == command.SrsDocumentId && x.ProjectId == command.ProjectId && !x.IsDeleted));

        if (doc == null)
        {
            throw new NotFoundException($"SrsDocument {command.SrsDocumentId} khong tim thay.");
        }

        // Validate there is text content to analyze
        var rawContent = doc.ParsedMarkdown ?? doc.RawContent ?? string.Empty;

        // For FileUpload documents without extracted text, try reading from storage
        if (string.IsNullOrWhiteSpace(rawContent) && doc.SourceType == SrsSourceType.FileUpload && doc.StorageFileId.HasValue)
        {
            try
            {
                var fileResult = await _storageFileGateway.DownloadAsync(doc.StorageFileId.Value, cancellationToken);
                if (fileResult?.Content != null && fileResult.Content.Length > 0)
                {
                    rawContent = Encoding.UTF8.GetString(fileResult.Content).Trim();
                    // Persist so we don't re-read next time
                    doc.RawContent = rawContent;
                    await _srsDocumentRepository.UpdateAsync(doc, cancellationToken);
                    await _srsDocumentRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not read file content from storage for SrsDocument {Id}", doc.Id);
            }
        }

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            throw new ValidationException(
                "Khong the phan tich tai lieu: khong co noi dung van ban. " +
                "Voi tai lieu FileUpload, vui long su dung sourceType=TextInput va dan truc tiep noi dung SRS.");
        }

        // Create the analysis job
        var job = new SrsAnalysisJob
        {
            SrsDocumentId = command.SrsDocumentId,
            Status = SrsAnalysisJobStatus.Queued,
            JobType = SrsAnalysisJobType.InitialAnalysis,
            TriggeredById = command.CurrentUserId,
            QueuedAt = DateTimeOffset.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };

        await _jobRepository.AddAsync(job, cancellationToken);

        // Update document status
        doc.AnalysisStatus = SrsAnalysisStatus.Processing;

        await _srsDocumentRepository.UpdateAsync(doc, cancellationToken);
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.JobId = job.Id;

        // Build n8n payload (no callbackUrl needed — synchronous response)
        var endpoints = await GetEndpointsAsync(doc, cancellationToken);

        var payload = new N8nSrsAnalysisPayload
        {
            SrsDocumentId = doc.Id,
            JobId = job.Id,
            RawContent = rawContent,
            ProjectContext = $"ProjectId: {doc.ProjectId}",
            Endpoints = endpoints,
        };

        // Call n8n synchronously — wait for full result (like LLM suggestion flow)
        job.Status = SrsAnalysisJobStatus.Triggering;
        job.TriggeredAt = DateTimeOffset.UtcNow;
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        SrsAnalysisCallbackRequest callbackData;
        try
        {
            callbackData = await _n8nService.TriggerWebhookAsync<N8nSrsAnalysisPayload, SrsAnalysisCallbackRequest>(
                WebhookName, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            var result = new WebhookTriggerResult { ErrorMessage = ex.Message, ErrorDetails = ex.ToString() };
            if (ShouldUseLocalFallback(result))
            {
                await ApplyLocalFallbackAsync(job, doc, rawContent, endpoints, cancellationToken);
                return;
            }
            else
            {
                job.Status = SrsAnalysisJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTimeOffset.UtcNow;
                doc.AnalysisStatus = SrsAnalysisStatus.Failed;
                await _srsDocumentRepository.UpdateAsync(doc, cancellationToken);
                await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
                _logger.LogError(ex, "SRS analysis n8n call failed. JobId={JobId}", job.Id);
                throw;
            }
        }

        // n8n returned results — process them via the callback handler
        await _dispatcher.DispatchAsync(new ProcessSrsAnalysisCallbackCommand
        {
            JobId = job.Id,
            Requirements = callbackData?.Requirements ?? new List<N8nSrsRequirementResult>(),
            ClarificationQuestions = callbackData?.ClarificationQuestions ?? new List<N8nSrsClarificationQuestion>(),
        }, cancellationToken);

        _logger.LogInformation(
            "SRS analysis completed synchronously. JobId={JobId}, SrsDocumentId={SrsDocumentId}",
            job.Id, command.SrsDocumentId);
    }

    private async Task ApplyLocalFallbackAsync(
        SrsAnalysisJob job,
        SrsDocument doc,
        string rawContent,
        List<N8nSrsEndpointRef> endpoints,
        CancellationToken cancellationToken)
    {
        var requirements = BuildLocalRequirements(rawContent, endpoints);
        var clarifications = BuildLocalClarifications(requirements);

        _logger.LogWarning(
            "SRS analysis webhook is unavailable. Falling back to local synthesis. JobId={JobId}, SrsDocumentId={SrsDocumentId}, RequirementCount={RequirementCount}",
            job.Id,
            doc.Id,
            requirements.Count);

        await _dispatcher.DispatchAsync(new ProcessSrsAnalysisCallbackCommand
        {
            JobId = job.Id,
            Requirements = requirements,
            ClarificationQuestions = clarifications,
        }, cancellationToken);
    }

    private static bool ShouldUseLocalFallback(WebhookTriggerResult result)
    {
        if (result == null)
        {
            return false;
        }

        return ContainsIgnoreCase(result.ErrorMessage, "Status: NotFound")
            || ContainsIgnoreCase(result.ErrorDetails, "not registered");
    }

    private static List<N8nSrsRequirementResult> BuildLocalRequirements(
        string rawContent,
        IReadOnlyList<N8nSrsEndpointRef> endpoints)
    {
        var candidates = ExtractRequirementCandidates(rawContent, endpoints);

        return candidates.Select((candidate, index) =>
        {
            var matchedEndpoint = MatchEndpoint(candidate, endpoints, index);
            var ambiguityNotes = BuildAmbiguityNotes(candidate);
            var constraint = BuildStructuredConstraint(candidate);

            return new N8nSrsRequirementResult
            {
                RequirementCode = $"REQ-{index + 1:000}",
                Title = Truncate(candidate, 120),
                Description = candidate,
                Type = InferRequirementType(candidate),
                TestableConstraints = JsonSerializer.Serialize(new[] { constraint }),
                Assumptions = JsonSerializer.Serialize(BuildAssumptions(candidate, matchedEndpoint)),
                Ambiguities = JsonSerializer.Serialize(ambiguityNotes),
                ConfidenceScore = ambiguityNotes.Count == 0 ? 0.78f : 0.62f,
                MappedEndpointPath = matchedEndpoint == null ? null : $"{matchedEndpoint.Method} {matchedEndpoint.Path}",
            };
        }).ToList();
    }

    private static List<N8nSrsClarificationQuestion> BuildLocalClarifications(
        IReadOnlyList<N8nSrsRequirementResult> requirements)
    {
        var target = requirements.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Description)
            && !HasActionableConstraint(x.Description));

        if (target == null)
        {
            return new List<N8nSrsClarificationQuestion>();
        }

        return new List<N8nSrsClarificationQuestion>
        {
            new()
            {
                RequirementCode = target.RequirementCode,
                AmbiguitySource = "Local fallback found requirement wording without a concrete expected outcome.",
                Question = "Requirement này cần status code hoặc điều kiện pass/fail cụ thể nào để sinh expected chính xác?",
                IsCritical = true,
                SuggestedOptions = JsonSerializer.Serialize(new[]
                {
                    "Bổ sung HTTP status mong đợi",
                    "Bổ sung field/validation cụ thể cần kiểm tra",
                    "Cho phép chỉ suy ra từ Swagger nếu SRS chưa nói rõ",
                }),
            },
        };
    }

    private static List<string> ExtractRequirementCandidates(
        string rawContent,
        IReadOnlyList<N8nSrsEndpointRef> endpoints)
    {
        var lines = (rawContent ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeCandidate)
            .Where(x => x.Length >= 12)
            .ToList();

        var candidates = new List<string>();
        foreach (var line in lines)
        {
            if (!LooksLikeRequirementCandidate(line))
            {
                continue;
            }

            if (!candidates.Contains(line, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(line);
            }

            if (candidates.Count >= 12)
            {
                break;
            }
        }

        if (candidates.Count > 0)
        {
            return candidates;
        }

        if (endpoints.Count > 0)
        {
            return endpoints
                .Take(4)
                .Select(x => $"{x.Method} {x.Path} must follow happy path, validation, and authorization rules defined by the API contract.")
                .ToList();
        }

        return new List<string>
        {
            "The system must enforce business flows and validation outcomes described in the SRS.",
        };
    }

    private static string NormalizeCandidate(string line)
    {
        var value = (line ?? string.Empty).Trim();
        value = value.TrimStart('-', '*', '#', ' ', '\t', '|');

        while (value.Length > 0 && char.IsDigit(value[0]))
        {
            value = value[1..];
        }

        value = value.TrimStart('.', ')', ':', '-', ' ');
        return value.Trim();
    }

    private static bool LooksLikeRequirementCandidate(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        if (line.StartsWith("```", StringComparison.Ordinal)
            || line.StartsWith("---", StringComparison.Ordinal)
            || line.StartsWith("===", StringComparison.Ordinal))
        {
            return false;
        }

        return line.Contains("must", StringComparison.OrdinalIgnoreCase)
            || line.Contains("should", StringComparison.OrdinalIgnoreCase)
            || line.Contains("required", StringComparison.OrdinalIgnoreCase)
            || line.Contains("validation", StringComparison.OrdinalIgnoreCase)
            || line.Contains("invalid", StringComparison.OrdinalIgnoreCase)
            || line.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
            || line.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || line.Contains("forbidden", StringComparison.OrdinalIgnoreCase)
            || line.Contains("not found", StringComparison.OrdinalIgnoreCase)
            || line.Contains("format", StringComparison.OrdinalIgnoreCase)
            || line.Contains("minimum", StringComparison.OrdinalIgnoreCase)
            || line.Contains("maximum", StringComparison.OrdinalIgnoreCase)
            || line.Contains("at least", StringComparison.OrdinalIgnoreCase)
            || line.Contains("at most", StringComparison.OrdinalIgnoreCase)
            || line.Contains(">=", StringComparison.OrdinalIgnoreCase)
            || line.Contains("<=", StringComparison.OrdinalIgnoreCase)
            || line.Contains("phải", StringComparison.OrdinalIgnoreCase)
            || line.Contains("không", StringComparison.OrdinalIgnoreCase)
            || line.Contains("tối thiểu", StringComparison.OrdinalIgnoreCase)
            || line.Contains("tối đa", StringComparison.OrdinalIgnoreCase)
            || line.Contains("bắt buộc", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> BuildStructuredConstraint(string candidate)
    {
        var expectedOutcome = ExtractExpectedOutcome(candidate);
        return new Dictionary<string, string>
        {
            ["constraint"] = candidate,
            ["expectedOutcome"] = expectedOutcome,
            ["priority"] = InferPriority(candidate),
            ["field"] = InferFieldName(candidate),
            ["ruleType"] = InferRuleType(candidate),
        };
    }

    private static string ExtractExpectedOutcome(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var status = StatusCodeRegex.Match(candidate);
        if (status.Success)
        {
            return status.Groups[1].Value;
        }

        var lower = candidate.ToLowerInvariant();
        if (lower.Contains("duplicate") || lower.Contains("already exists") || lower.Contains("conflict"))
        {
            return "409";
        }

        if (lower.Contains("unauthorized"))
        {
            return "401";
        }

        if (lower.Contains("forbidden"))
        {
            return "403";
        }

        if (lower.Contains("not found"))
        {
            return "404";
        }

        if (lower.Contains("invalid")
            || lower.Contains("required")
            || lower.Contains("validation")
            || lower.Contains("minlength")
            || lower.Contains("maxLength")
            || lower.Contains("format")
            || lower.Contains("tối thiểu")
            || lower.Contains("tối đa")
            || lower.Contains("bắt buộc"))
        {
            return "400";
        }

        if (lower.Contains("success")
            || lower.Contains("created")
            || lower.Contains("registered")
            || lower.Contains("login"))
        {
            return "200/201";
        }

        return null;
    }

    private static string InferFieldName(string candidate)
    {
        var lower = candidate?.ToLowerInvariant() ?? string.Empty;
        foreach (var field in new[] { "email", "password", "username", "name", "price", "stock", "categoryid", "category", "token" })
        {
            if (lower.Contains(field, StringComparison.OrdinalIgnoreCase))
            {
                return field == "categoryid" ? "categoryId" : field;
            }
        }

        return "request";
    }

    private static string InferRuleType(string candidate)
    {
        var lower = candidate?.ToLowerInvariant() ?? string.Empty;
        if (lower.Contains("duplicate") || lower.Contains("already exists") || lower.Contains("unique"))
        {
            return "uniqueness";
        }

        if (lower.Contains("format") || lower.Contains("email"))
        {
            return "format";
        }

        if (MinLengthRegex.IsMatch(lower) || MaxLengthRegex.IsMatch(lower) || lower.Contains("minimum") || lower.Contains("maximum") || lower.Contains("tối thiểu") || lower.Contains("tối đa"))
        {
            return "boundary";
        }

        if (lower.Contains("required") || lower.Contains("bắt buộc") || lower.Contains("missing"))
        {
            return "required";
        }

        if (lower.Contains("unauthorized") || lower.Contains("forbidden") || lower.Contains("authorization") || lower.Contains("token"))
        {
            return "authorization";
        }

        return "behavior";
    }

    private static bool HasActionableConstraint(string candidate)
    {
        return !string.IsNullOrWhiteSpace(ExtractExpectedOutcome(candidate))
            || MinLengthRegex.IsMatch(candidate ?? string.Empty)
            || MaxLengthRegex.IsMatch(candidate ?? string.Empty)
            || candidate?.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true
            || candidate?.Contains("unique", StringComparison.OrdinalIgnoreCase) == true
            || candidate?.Contains("required", StringComparison.OrdinalIgnoreCase) == true
            || candidate?.Contains("format", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static List<string> BuildAssumptions(string candidate, N8nSrsEndpointRef endpoint)
    {
        var assumptions = new List<string>();

        if (endpoint != null)
        {
            assumptions.Add($"Mapped to {endpoint.Method} {endpoint.Path}");
        }

        if (ContainsIgnoreCase(candidate, "login") || ContainsIgnoreCase(candidate, "register"))
        {
            assumptions.Add("Authentication endpoints use the configured runtime base URL.");
        }

        if (!HasActionableConstraint(candidate))
        {
            assumptions.Add("Expected outcome may need clarification if Swagger does not define it clearly.");
        }

        return assumptions;
    }

    private static List<string> BuildAmbiguityNotes(string candidate)
    {
        var notes = new List<string>();

        if (!HasActionableConstraint(candidate))
        {
            notes.Add("Missing explicit pass/fail outcome in fallback requirement text.");
        }

        return notes;
    }

    private static N8nSrsEndpointRef MatchEndpoint(
        string candidate,
        IReadOnlyList<N8nSrsEndpointRef> endpoints,
        int index)
    {
        if (endpoints.Count == 0)
        {
            return null;
        }

        var endpoint = endpoints.FirstOrDefault(x => ContainsIgnoreCase(candidate, x.Path));
        if (endpoint != null)
        {
            return endpoint;
        }

        if (ContainsIgnoreCase(candidate, "login") || ContainsIgnoreCase(candidate, "auth"))
        {
            endpoint = endpoints.FirstOrDefault(x => ContainsIgnoreCase(x.Path, "login") || ContainsIgnoreCase(x.Path, "auth"));
            if (endpoint != null)
            {
                return endpoint;
            }
        }

        if (ContainsIgnoreCase(candidate, "register")
            || ContainsIgnoreCase(candidate, "create account")
            || ContainsIgnoreCase(candidate, "tai khoan"))
        {
            endpoint = endpoints.FirstOrDefault(x => ContainsIgnoreCase(x.Path, "register")
                || ContainsIgnoreCase(x.Path, "signup")
                || ContainsIgnoreCase(x.Path, "users"));
            if (endpoint != null)
            {
                return endpoint;
            }
        }

        return endpoints[index % endpoints.Count];
    }

    private static string InferRequirementType(string candidate)
    {
        if (ContainsIgnoreCase(candidate, "auth")
            || ContainsIgnoreCase(candidate, "login")
            || ContainsIgnoreCase(candidate, "token")
            || ContainsIgnoreCase(candidate, "password")
            || ContainsIgnoreCase(candidate, "authorization"))
        {
            return "security";
        }

        if (ContainsIgnoreCase(candidate, "performance")
            || ContainsIgnoreCase(candidate, "latency")
            || ContainsIgnoreCase(candidate, "timeout")
            || ContainsIgnoreCase(candidate, "throughput"))
        {
            return "performance";
        }

        if (ContainsIgnoreCase(candidate, "required")
            || ContainsIgnoreCase(candidate, "validation")
            || ContainsIgnoreCase(candidate, "format")
            || ContainsIgnoreCase(candidate, "must"))
        {
            return "constraint";
        }

        return "functional";
    }

    private static string InferPriority(string candidate)
    {
        return ContainsIgnoreCase(candidate, "auth")
            || ContainsIgnoreCase(candidate, "login")
            || ContainsIgnoreCase(candidate, "register")
            || ContainsIgnoreCase(candidate, "password")
            ? "High"
            : "Medium";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..(maxLength - 3)] + "...";
    }

    private static bool ContainsIgnoreCase(string input, string value)
    {
        return !string.IsNullOrWhiteSpace(input)
            && !string.IsNullOrWhiteSpace(value)
            && input.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<N8nSrsEndpointRef>> GetEndpointsAsync(SrsDocument doc, CancellationToken cancellationToken)
    {
        if (doc.TestSuiteId == null)
        {
            return new List<N8nSrsEndpointRef>();
        }

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == doc.TestSuiteId.Value));

        if (suite?.ApiSpecId == null)
        {
            return new List<N8nSrsEndpointRef>();
        }

        try
        {
            var metadata = await _endpointMetadataService.GetEndpointMetadataAsync(
                suite.ApiSpecId.Value,
                suite.SelectedEndpointIds?.Count > 0
                    ? (IReadOnlyCollection<Guid>)suite.SelectedEndpointIds
                    : null,
                cancellationToken);

            return metadata.Select(e => new N8nSrsEndpointRef
            {
                EndpointId = e.EndpointId,
                Method = e.HttpMethod,
                Path = e.Path,
                Summary = e.OperationId,
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load endpoints for SRS analysis. SrsDocumentId={SrsDocumentId}", doc.Id);
            return new List<N8nSrsEndpointRef>();
        }
    }
}

// n8n payload models for Phase 1
public class N8nSrsAnalysisPayload
{
    [JsonPropertyName("srsDocumentId")]
    public Guid SrsDocumentId { get; set; }

    [JsonPropertyName("jobId")]
    public Guid JobId { get; set; }

    [JsonPropertyName("rawContent")]
    public string RawContent { get; set; }

    [JsonPropertyName("projectContext")]
    public string ProjectContext { get; set; }

    [JsonPropertyName("endpoints")]
    public List<N8nSrsEndpointRef> Endpoints { get; set; } = new();

    [JsonPropertyName("callbackUrl")]
    public string CallbackUrl { get; set; }

    [JsonPropertyName("callbackApiKey")]
    public string CallbackApiKey { get; set; }
}

public class N8nSrsEndpointRef
{
    [JsonPropertyName("endpointId")]
    public Guid EndpointId { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; }
}
