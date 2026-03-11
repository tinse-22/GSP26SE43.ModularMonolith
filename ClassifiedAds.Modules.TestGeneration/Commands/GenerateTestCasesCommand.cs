using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class GenerateTestCasesCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GenerateTestCasesCommandHandler : ICommandHandler<GenerateTestCasesCommand>
{
    private const string WebhookName = "DotnetIntegration";

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestOrderProposal, Guid> _proposalRepository;
    private readonly IN8nIntegrationService _n8nService;
    private readonly IApiTestOrderService _apiTestOrderService;
    private readonly ILogger<GenerateTestCasesCommandHandler> _logger;

    public GenerateTestCasesCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestOrderProposal, Guid> proposalRepository,
        IN8nIntegrationService n8nService,
        IApiTestOrderService apiTestOrderService,
        ILogger<GenerateTestCasesCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _proposalRepository = proposalRepository;
        _n8nService = n8nService;
        _apiTestOrderService = apiTestOrderService;
        _logger = logger;
    }

    public async Task HandleAsync(GenerateTestCasesCommand command, CancellationToken cancellationToken = default)
    {
        if (command.TestSuiteId == Guid.Empty)
            throw new ValidationException("TestSuiteId là bắt buộc.");

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
            throw new NotFoundException($"Không tìm thấy test suite '{command.TestSuiteId}'.");

        if (suite.CreatedById != command.CurrentUserId)
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");

        // Get the latest approved proposal to get the applied order
        var approvedProposal = await _proposalRepository.FirstOrDefaultAsync(
            _proposalRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId
                    && (x.Status == ProposalStatus.Approved
                        || x.Status == ProposalStatus.ModifiedAndApproved))
                .OrderByDescending(x => x.ProposalNumber));

        if (approvedProposal == null)
            throw new ValidationException("Cần approve test order trước khi generate test cases.");

        var appliedOrder = _apiTestOrderService.DeserializeOrderJson(approvedProposal.AppliedOrder);

        var payload = new N8nGenerateTestsPayload
        {
            TestSuiteId = suite.Id,
            SpecificationId = suite.ApiSpecId ?? Guid.Empty,
            AppliedOrder = appliedOrder.Select(x => new N8nOrderedEndpoint
            {
                EndpointId = x.EndpointId,
                HttpMethod = x.HttpMethod,
                Path = x.Path,
                OrderIndex = x.OrderIndex,
                DependsOnEndpointIds = x.DependsOnEndpointIds?.ToList() ?? new(),
                ReasonCodes = x.ReasonCodes?.ToList() ?? new(),
            }).ToList(),
            EndpointBusinessContexts = suite.EndpointBusinessContexts ?? new(),
            GlobalBusinessRules = suite.GlobalBusinessRules,
        };

        await _n8nService.TriggerWebhookAsync(WebhookName, payload, cancellationToken);

        _logger.LogInformation(
            "Triggered test generation via n8n. TestSuiteId={TestSuiteId}, EndpointCount={EndpointCount}, ActorUserId={ActorUserId}",
            suite.Id, appliedOrder.Count, command.CurrentUserId);
    }
}

public class N8nGenerateTestsPayload
{
    public Guid TestSuiteId { get; set; }
    public Guid SpecificationId { get; set; }
    public List<N8nOrderedEndpoint> AppliedOrder { get; set; } = new();
    public Dictionary<Guid, string> EndpointBusinessContexts { get; set; } = new();
    public string GlobalBusinessRules { get; set; }
}

public class N8nOrderedEndpoint
{
    public Guid EndpointId { get; set; }
    public string HttpMethod { get; set; }
    public string Path { get; set; }
    public int OrderIndex { get; set; }
    public List<Guid> DependsOnEndpointIds { get; set; } = new();
    public List<string> ReasonCodes { get; set; } = new();
}
