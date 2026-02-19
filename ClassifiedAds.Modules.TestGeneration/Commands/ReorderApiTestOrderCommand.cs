using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Constants;
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

public class ReorderApiTestOrderCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid ProposalId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string RowVersion { get; set; }

    public IReadOnlyCollection<Guid> OrderedEndpointIds { get; set; } = Array.Empty<Guid>();

    public string ReviewNotes { get; set; }

    public ApiTestOrderProposalModel Result { get; set; }
}

public class ReorderApiTestOrderCommandHandler : ICommandHandler<ReorderApiTestOrderCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestOrderProposal, Guid> _proposalRepository;
    private readonly IApiTestOrderService _apiTestOrderService;
    private readonly ILogger<ReorderApiTestOrderCommandHandler> _logger;

    public ReorderApiTestOrderCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestOrderProposal, Guid> proposalRepository,
        IApiTestOrderService apiTestOrderService,
        ILogger<ReorderApiTestOrderCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _proposalRepository = proposalRepository;
        _apiTestOrderService = apiTestOrderService;
        _logger = logger;
    }

    public async Task HandleAsync(ReorderApiTestOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (command.ProposalId == Guid.Empty)
        {
            throw new ValidationException("ProposalId là bắt buộc.");
        }

        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == command.TestSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");
        }

        EnsureOwnership(suite, command.CurrentUserId);

        var proposal = await _proposalRepository.FirstOrDefaultAsync(
            _proposalRepository.GetQueryableSet()
                .Where(x => x.Id == command.ProposalId && x.TestSuiteId == command.TestSuiteId));

        if (proposal == null)
        {
            throw new NotFoundException($"Không tìm thấy proposal với mã '{command.ProposalId}'.");
        }

        EnsurePendingProposal(proposal);
        _proposalRepository.SetRowVersion(proposal, ApiTestOrderModelMapper.ParseRowVersion(command.RowVersion));

        var proposedOrder = _apiTestOrderService.DeserializeOrderJson(proposal.ProposedOrder);
        var normalizedOrderedIds = _apiTestOrderService.ValidateReorderedEndpointSet(
            proposedOrder,
            command.OrderedEndpointIds);

        var proposedOrderByEndpointId = proposedOrder.ToDictionary(x => x.EndpointId);
        var reorderedItems = normalizedOrderedIds
            .Select((endpointId, index) =>
            {
                var item = proposedOrderByEndpointId[endpointId];
                return new ApiOrderItemModel
                {
                    EndpointId = item.EndpointId,
                    HttpMethod = item.HttpMethod,
                    Path = item.Path,
                    OrderIndex = index + 1,
                    DependsOnEndpointIds = item.DependsOnEndpointIds,
                    ReasonCodes = item.ReasonCodes,
                    IsAuthRelated = item.IsAuthRelated,
                };
            })
            .ToList();

        proposal.UserModifiedOrder = _apiTestOrderService.SerializeOrderJson(reorderedItems);
        proposal.ReviewNotes = string.IsNullOrWhiteSpace(command.ReviewNotes) ? null : command.ReviewNotes.Trim();

        await _proposalRepository.UpdateAsync(proposal, cancellationToken);

        try
        {
            await _proposalRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (_proposalRepository.IsDbUpdateConcurrencyException(ex))
        {
            throw new ConflictException(
                TestOrderReasonCodes.ConcurrencyConflict,
                "Dữ liệu proposal đã thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
        }

        command.Result = ApiTestOrderModelMapper.ToModel(proposal, _apiTestOrderService);
        _logger.LogInformation(
            "Reordered test order proposal. TestSuiteId={TestSuiteId}, ProposalId={ProposalId}, ProposalNumber={ProposalNumber}, ActorUserId={ActorUserId}",
            command.TestSuiteId,
            command.ProposalId,
            proposal.ProposalNumber,
            command.CurrentUserId);
    }

    private static void EnsureOwnership(TestSuite suite, Guid currentUserId)
    {
        if (suite.CreatedById != currentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }
    }

    private static void EnsurePendingProposal(TestOrderProposal proposal)
    {
        if (proposal.Status != ProposalStatus.Pending)
        {
            throw new ValidationException("Chỉ có thể reorder proposal ở trạng thái Pending.");
        }
    }
}
