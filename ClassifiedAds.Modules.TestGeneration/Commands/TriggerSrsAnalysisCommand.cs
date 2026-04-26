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
using System.Text.Json.Serialization;
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
        else if (ShouldUseLocalFallback(result))
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

            return new N8nSrsRequirementResult
            {
                RequirementCode = $"REQ-{index + 1:000}",
                Title = Truncate(candidate, 120),
                Description = candidate,
                Type = InferRequirementType(candidate),
                TestableConstraints = JsonSerializer.Serialize(new[]
                {
                    new Dictionary<string, string>
                    {
                        ["constraint"] = candidate,
                        ["priority"] = InferPriority(candidate),
                    },
                }),
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
        var target = requirements.FirstOrDefault(x => ContainsIgnoreCase(x.Description, "register")
            || ContainsIgnoreCase(x.Description, "create account")
            || ContainsIgnoreCase(x.Description, "tai khoan"))
            ?? requirements.FirstOrDefault();

        if (target == null)
        {
            return new List<N8nSrsClarificationQuestion>();
        }

        return new List<N8nSrsClarificationQuestion>
        {
            new()
            {
                RequirementCode = target.RequirementCode,
                AmbiguitySource = "Can xac nhan thong tin test account can dung khi tao tai khoan.",
                Question = "Khi tao tai khoan trong SUT, co phai dung email tinvtse@gmail.com va password User@123 khong?",
                IsCritical = true,
                SuggestedOptions = JsonSerializer.Serialize(new[]
                {
                    "Su dung tinvtse@gmail.com / User@123",
                    "Dung thong tin khac do nguoi dung cung cap",
                }),
            },
        };
    }

    private static List<string> ExtractRequirementCandidates(
        string rawContent,
        IReadOnlyList<N8nSrsEndpointRef> endpoints)
    {
        var candidates = (rawContent ?? string.Empty)
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeCandidate)
            .Where(x => x.Length >= 20)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        if (candidates.Count > 0)
        {
            return candidates;
        }

        if (endpoints.Count > 0)
        {
            return endpoints
                .Take(4)
                .Select(x => $"{x.Method} {x.Path} phai xu ly happy path, validation, va authorization dung theo hop dong API.")
                .ToList();
        }

        return new List<string>
        {
            "He thong phai thuc thi dung cac luong nghiep vu chinh va xu ly loi theo noi dung SRS da cung cap.",
        };
    }

    private static string NormalizeCandidate(string line)
    {
        var value = (line ?? string.Empty).Trim();
        value = value.TrimStart('-', '*', '#', ' ', '\t');

        while (value.Length > 0 && char.IsDigit(value[0]))
        {
            value = value[1..];
        }

        value = value.TrimStart('.', ')', ':', '-', ' ');
        return value.Trim();
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

        return assumptions;
    }

    private static List<string> BuildAmbiguityNotes(string candidate)
    {
        var notes = new List<string>();

        if (ContainsIgnoreCase(candidate, "register")
            || ContainsIgnoreCase(candidate, "create account")
            || ContainsIgnoreCase(candidate, "tai khoan"))
        {
            notes.Add("Can xac nhan bo thong tin test account can dung cho luong tao tai khoan.");
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
