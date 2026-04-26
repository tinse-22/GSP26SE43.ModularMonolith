using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Infrastructure.Messaging;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.MessageBusMessages;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class GenerateTestCasesCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }

    /// <summary>
    /// Output: The job ID for tracking generation status.
    /// </summary>
    public Guid JobId { get; set; }
}

public class GenerateTestCasesCommandHandler : ICommandHandler<GenerateTestCasesCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestOrderProposal, Guid> _proposalRepository;
    private readonly IRepository<TestGenerationJob, Guid> _jobRepository;
    private readonly ITestGenerationPayloadBuilder _payloadBuilder;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<GenerateTestCasesCommandHandler> _logger;

    public GenerateTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestOrderProposal, Guid> proposalRepository,
        IRepository<TestGenerationJob, Guid> jobRepository,
        ITestGenerationPayloadBuilder payloadBuilder,
        IMessageBus messageBus,
        ILogger<GenerateTestCasesCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _proposalRepository = proposalRepository;
        _jobRepository = jobRepository;
        _payloadBuilder = payloadBuilder;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task HandleAsync(GenerateTestCasesCommand command, CancellationToken cancellationToken = default)
    {
        if (command.TestSuiteId == Guid.Empty)
        {
            throw new ValidationException("TestSuiteId là bắt buộc.");
        }

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite '{command.TestSuiteId}'.");
        }

        if (suite.CreatedById != command.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        // Get the latest approved proposal to get the applied order
        var approvedProposal = await _proposalRepository.FirstOrDefaultAsync(
            _proposalRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId
                    && (x.Status == ProposalStatus.Approved
                        || x.Status == ProposalStatus.ModifiedAndApproved))
                .OrderByDescending(x => x.ProposalNumber));

        if (approvedProposal == null)
        {
            throw new ValidationException("Cần approve test order trước khi generate test cases.");
        }

        _logger.LogInformation(
            "Creating test generation job. TestSuiteId={TestSuiteId}, ProposalId={ProposalId}",
            command.TestSuiteId, approvedProposal.Id);

        // Create job record for tracking
        var job = new TestGenerationJob
        {
            TestSuiteId = command.TestSuiteId,
            ProposalId = approvedProposal.Id,
            Status = GenerationJobStatus.Queued,
            TriggeredById = command.CurrentUserId,
            QueuedAt = DateTimeOffset.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray(),
            WebhookName = _payloadBuilder.WebhookName,
        };

        await _jobRepository.AddAsync(job, cancellationToken);
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        // Set output for controller to return
        command.JobId = job.Id;

        _logger.LogInformation(
            "Test generation job created. JobId={JobId}, TestSuiteId={TestSuiteId}",
            job.Id, command.TestSuiteId);

        // Queue background trigger so the API can return 202 without waiting on n8n.
        try
        {
            await _messageBus.SendAsync(
                new TriggerTestGenerationMessage
                {
                    JobId = job.Id,
                    TestSuiteId = command.TestSuiteId,
                    ProposalId = approvedProposal.Id,
                    TriggeredById = command.CurrentUserId,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                new MetaData
                {
                    ActivityId = Activity.Current?.Id,
                    CreationDateTime = DateTimeOffset.UtcNow,
                    EnqueuedDateTime = DateTimeOffset.UtcNow,
                    MessageId = job.Id.ToString(),
                },
                cancellationToken);

            _logger.LogInformation(
                "Queued test generation trigger message. JobId={JobId}, TestSuiteId={TestSuiteId}, ProposalId={ProposalId}, WebhookName={WebhookName}",
                job.Id, command.TestSuiteId, approvedProposal.Id, _payloadBuilder.WebhookName);
        }
        catch (Exception ex)
        {
            job.Status = GenerationJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = $"Không thể đưa yêu cầu trigger n8n vào hàng đợi: {ex.Message}";
            job.ErrorDetails = ex.ToString();
            job.RowVersion = Guid.NewGuid().ToByteArray();
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogError(
                ex,
                "Failed to queue test generation trigger message. JobId={JobId}, TestSuiteId={TestSuiteId}",
                job.Id, command.TestSuiteId);

            throw new ValidationException(
                $"Không thể đưa yêu cầu trigger n8n vào hàng đợi. JobId={job.Id}. Lỗi: {ex.Message}",
                ex);
        }
    }
}

public class N8nGenerateTestsPayload
{
    public Guid TestSuiteId { get; set; }
    public string TestSuiteName { get; set; } = string.Empty;
    public Guid SpecificationId { get; set; }
    public N8nUnifiedPromptConfig PromptConfig { get; set; } = new ();
    public List<N8nOrderedEndpoint> Endpoints { get; set; } = new ();
    public Dictionary<Guid, string> EndpointBusinessContexts { get; set; } = new ();
    public string GlobalBusinessRules { get; set; }

    /// <summary>BE endpoint n8n should POST generated test cases back to.</summary>
    public string CallbackUrl { get; set; } = string.Empty;

    /// <summary>Sent back by n8n in the x-callback-api-key header so BE can validate it.</summary>
    public string CallbackApiKey { get; set; } = string.Empty;

    /// <summary>
    /// SRS requirements linked to this test suite. Present when the suite has a linked SRS document.
    /// LLM should use these to populate coveredRequirementIds per test case.
    /// </summary>
    public List<N8nSrsRequirement> SrsRequirements { get; set; } = new();
}

public class N8nSrsRequirement
{
    public Guid Id { get; set; }
    public string Code { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
}

public class N8nOrderedEndpoint
{
    public Guid EndpointId { get; set; }
    public string HttpMethod { get; set; }
    public string Path { get; set; }
    public string OperationId { get; set; }
    public int OrderIndex { get; set; }
    public List<Guid> DependsOnEndpointIds { get; set; } = new ();
    public List<string> ReasonCodes { get; set; } = new ();
    public bool IsAuthRelated { get; set; }
    public string BusinessContext { get; set; }
    public Models.N8nPromptPayload Prompt { get; set; }
    public List<string> ParameterSchemaPayloads { get; set; } = new ();
    public List<string> ResponseSchemaPayloads { get; set; } = new ();
}

public class N8nUnifiedPromptConfig
{
    public string SystemPrompt { get; set; }
    public string TaskInstruction { get; set; }
    public string Rules { get; set; }
    public string ResponseFormat { get; set; }
}
