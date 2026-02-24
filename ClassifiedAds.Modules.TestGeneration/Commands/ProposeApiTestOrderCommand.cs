using ClassifiedAds.Application;
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

public class ProposeApiTestOrderCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid CurrentUserId { get; set; }

    public Guid SpecificationId { get; set; }

    public IReadOnlyCollection<Guid> SelectedEndpointIds { get; set; } = Array.Empty<Guid>();

    public ProposalSource Source { get; set; } = ProposalSource.Ai;

    public string LlmModel { get; set; }

    public string ReasoningNote { get; set; }

    public ApiTestOrderProposalModel Result { get; set; }
}

public class ProposeApiTestOrderCommandHandler : ICommandHandler<ProposeApiTestOrderCommand>
{
    private static readonly ProposalStatus[] SupersedableStatuses =
    {
        ProposalStatus.Pending,
        ProposalStatus.Approved,
        ProposalStatus.ModifiedAndApproved,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestOrderProposal, Guid> _proposalRepository;
    private readonly IApiTestOrderService _apiTestOrderService;
    private readonly ILogger<ProposeApiTestOrderCommandHandler> _logger;

    public ProposeApiTestOrderCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestOrderProposal, Guid> proposalRepository,
        IApiTestOrderService apiTestOrderService,
        ILogger<ProposeApiTestOrderCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _proposalRepository = proposalRepository;
        _apiTestOrderService = apiTestOrderService;
        _logger = logger;
    }

    public async Task HandleAsync(ProposeApiTestOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (command.TestSuiteId == Guid.Empty)
        {
            throw new ValidationException("TestSuiteId là bắt buộc.");
        }

        if (command.CurrentUserId == Guid.Empty)
        {
            throw new ValidationException("CurrentUserId là bắt buộc.");
        }

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");
        }

        EnsureOwnership(suite, command.CurrentUserId);
        EnsureSuiteSpecification(suite, command.SpecificationId);

        // FE-04-01 fallback: use persisted suite scope when request has no endpoint selection
        var effectiveEndpointIds = command.SelectedEndpointIds;
        if (effectiveEndpointIds == null || effectiveEndpointIds.Count == 0)
        {
            effectiveEndpointIds = suite.SelectedEndpointIds;
        }

        var order = await _apiTestOrderService.BuildProposalOrderAsync(
            command.TestSuiteId,
            command.SpecificationId,
            effectiveEndpointIds,
            cancellationToken);

        var existingProposals = await _proposalRepository.ToListAsync(
            _proposalRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == command.TestSuiteId)
                .OrderByDescending(x => x.ProposalNumber));

        foreach (var proposal in existingProposals.Where(x => SupersedableStatuses.Contains(x.Status)))
        {
            proposal.Status = ProposalStatus.Superseded;
            await _proposalRepository.UpdateAsync(proposal, cancellationToken);
        }

        var proposalNumber = existingProposals.Count == 0
            ? 1
            : existingProposals.Max(x => x.ProposalNumber) + 1;

        var newProposal = new TestOrderProposal
        {
            TestSuiteId = command.TestSuiteId,
            ProposalNumber = proposalNumber,
            Source = command.Source,
            Status = ProposalStatus.Pending,
            ProposedOrder = _apiTestOrderService.SerializeOrderJson(order),
            AiReasoning = string.IsNullOrWhiteSpace(command.ReasoningNote) ? null : command.ReasoningNote.Trim(),
            LlmModel = string.IsNullOrWhiteSpace(command.LlmModel) ? null : command.LlmModel.Trim(),
            RowVersion = Guid.NewGuid().ToByteArray(),
        };

        await _proposalRepository.AddAsync(newProposal, cancellationToken);

        suite.ApprovalStatus = ApprovalStatus.PendingReview;
        suite.ApprovedById = null;
        suite.ApprovedAt = null;
        suite.LastModifiedById = command.CurrentUserId;
        suite.RowVersion = Guid.NewGuid().ToByteArray();
        await _suiteRepository.UpdateAsync(suite, cancellationToken);

        await _proposalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = ApiTestOrderModelMapper.ToModel(newProposal, _apiTestOrderService);
        _logger.LogInformation(
            "Created test order proposal. TestSuiteId={TestSuiteId}, ProposalId={ProposalId}, ProposalNumber={ProposalNumber}, ActorUserId={ActorUserId}",
            command.TestSuiteId,
            newProposal.Id,
            newProposal.ProposalNumber,
            command.CurrentUserId);
    }

    private static void EnsureOwnership(TestSuite suite, Guid currentUserId)
    {
        if (suite.CreatedById != currentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }
    }

    private static void EnsureSuiteSpecification(TestSuite suite, Guid specificationId)
    {
        if (specificationId == Guid.Empty)
        {
            throw new ValidationException("SpecificationId là bắt buộc.");
        }

        if (!suite.ApiSpecId.HasValue || suite.ApiSpecId.Value != specificationId)
        {
            throw new ValidationException("Specification không khớp với test suite.");
        }
    }
}
