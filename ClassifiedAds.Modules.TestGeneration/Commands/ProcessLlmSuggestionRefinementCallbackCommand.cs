using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class ProcessLlmSuggestionRefinementCallbackCommand : ICommand
{
    public Guid JobId { get; set; }

    public N8nBoundaryNegativeResponse Response { get; set; }
}

public class ProcessLlmSuggestionRefinementCallbackCommandHandler : ICommandHandler<ProcessLlmSuggestionRefinementCallbackCommand>
{
    private readonly IRepository<TestGenerationJob, Guid> _jobRepository;
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<SrsRequirement, Guid> _srsRequirementRepository;
    private readonly IApiTestOrderGateService _gateService;
    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly IApiEndpointParameterDetailService _endpointParameterDetailService;
    private readonly ILlmScenarioSuggester _llmSuggester;
    private readonly ILlmSuggestionPreviewPersistenceService _persistenceService;
    private readonly ISubscriptionLimitGatewayService _subscriptionLimitService;
    private readonly ILogger<ProcessLlmSuggestionRefinementCallbackCommandHandler> _logger;

    public ProcessLlmSuggestionRefinementCallbackCommandHandler(
        IRepository<TestGenerationJob, Guid> jobRepository,
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<SrsRequirement, Guid> srsRequirementRepository,
        IApiTestOrderGateService gateService,
        IApiEndpointMetadataService endpointMetadataService,
        IApiEndpointParameterDetailService endpointParameterDetailService,
        ILlmScenarioSuggester llmSuggester,
        ILlmSuggestionPreviewPersistenceService persistenceService,
        ISubscriptionLimitGatewayService subscriptionLimitService,
        ILogger<ProcessLlmSuggestionRefinementCallbackCommandHandler> logger)
    {
        _jobRepository = jobRepository;
        _suiteRepository = suiteRepository;
        _srsDocumentRepository = srsDocumentRepository;
        _srsRequirementRepository = srsRequirementRepository;
        _gateService = gateService;
        _endpointMetadataService = endpointMetadataService;
        _endpointParameterDetailService = endpointParameterDetailService;
        _llmSuggester = llmSuggester;
        _persistenceService = persistenceService;
        _subscriptionLimitService = subscriptionLimitService;
        _logger = logger;
    }

    public async Task HandleAsync(
        ProcessLlmSuggestionRefinementCallbackCommand command,
        CancellationToken cancellationToken = default)
    {
        var job = await _jobRepository.FirstOrDefaultAsync(
            _jobRepository.GetQueryableSet().Where(x => x.Id == command.JobId));

        if (job == null)
        {
            throw new NotFoundException($"LLM suggestion refinement job '{command.JobId}' không tồn tại.");
        }

        if (job.Status == GenerationJobStatus.Completed
            || job.Status == GenerationJobStatus.Failed
            || job.Status == GenerationJobStatus.Cancelled)
        {
            _logger.LogWarning(
                "Ignoring LLM suggestion refinement callback for terminal job. JobId={JobId}, Status={Status}",
                job.Id,
                job.Status);
            return;
        }

        if (command.Response?.Scenarios == null || command.Response.Scenarios.Count == 0)
        {
            job.Status = GenerationJobStatus.Failed;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.ErrorMessage = "n8n refinement callback không có scenarios.";
            job.RowVersion = Guid.NewGuid().ToByteArray();
            await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == job.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{job.TestSuiteId}'.");
        }

        var approvedOrder = await _gateService.RequireApprovedOrderAsync(job.TestSuiteId, cancellationToken);
        var specificationId = suite.ApiSpecId ?? Guid.Empty;
        ValidationException.Requires(specificationId != Guid.Empty, "Test suite chưa liên kết API specification.");

        var endpointIds = approvedOrder.Select(x => x.EndpointId).Distinct().ToList();
        var endpointMetadata = await _endpointMetadataService.GetEndpointMetadataAsync(
            specificationId,
            endpointIds,
            cancellationToken);
        var endpointParameterDetails = await _endpointParameterDetailService.GetParameterDetailsAsync(
            specificationId,
            endpointIds,
            cancellationToken);

        SrsDocument srsDocument = null;
        List<SrsRequirement> srsRequirements = new();
        if (suite.SrsDocumentId.HasValue)
        {
            srsDocument = await _srsDocumentRepository.FirstOrDefaultAsync(
                _srsDocumentRepository.GetQueryableSet().Where(x => x.Id == suite.SrsDocumentId.Value));

            if (srsDocument != null)
            {
                srsRequirements = await _srsRequirementRepository.ToListAsync(
                    _srsRequirementRepository.GetQueryableSet().Where(x => x.SrsDocumentId == srsDocument.Id));
            }
        }

        var llmContext = new LlmScenarioSuggestionContext
        {
            TestSuiteId = suite.Id,
            UserId = job.TriggeredById,
            Suite = suite,
            EndpointMetadata = endpointMetadata,
            OrderedEndpoints = approvedOrder,
            SpecificationId = specificationId,
            EndpointParameterDetails = endpointParameterDetails.ToDictionary(x => x.EndpointId),
            AlgorithmProfile = new GenerationAlgorithmProfile(),
            BypassCache = true,
            SrsDocument = srsDocument,
            SrsRequirements = srsRequirements,
        };

        var llmResult = _llmSuggester.ParseRefinementResponse(llmContext, command.Response);
        var suggestions = await _persistenceService.ReplacePendingSuggestionsAsync(
            suite,
            approvedOrder,
            llmResult,
            job.TriggeredById,
            cancellationToken);

        await _subscriptionLimitService.IncrementUsageAsync(
            new IncrementUsageRequest
            {
                UserId = job.TriggeredById,
                LimitType = LimitType.MaxLlmCallsPerMonth,
                IncrementValue = 1,
            },
            cancellationToken);

        job.Status = GenerationJobStatus.Completed;
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.TestCasesGenerated = suggestions.Count;
        job.RowVersion = Guid.NewGuid().ToByteArray();
        await _jobRepository.UpdateAsync(job, cancellationToken);
        await _jobRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Processed LLM suggestion refinement callback. JobId={JobId}, TestSuiteId={TestSuiteId}, SuggestionCount={SuggestionCount}, Model={Model}",
            job.Id,
            job.TestSuiteId,
            suggestions.Count,
            llmResult.LlmModel);
    }
}
