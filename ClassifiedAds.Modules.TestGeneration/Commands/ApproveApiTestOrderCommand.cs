using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class ApproveApiTestOrderCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid ProposalId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string RowVersion { get; set; }

    public string ReviewNotes { get; set; }

    public ApiTestOrderProposalModel Result { get; set; }
}

public class ApproveApiTestOrderCommandHandler : ICommandHandler<ApproveApiTestOrderCommand>
{
    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestOrderProposal, Guid> _proposalRepository;
    private readonly IApiTestOrderService _apiTestOrderService;
    private readonly ILogger<ApproveApiTestOrderCommandHandler> _logger;

    public ApproveApiTestOrderCommandHandler(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestOrderProposal, Guid> proposalRepository,
        IApiTestOrderService apiTestOrderService,
        ILogger<ApproveApiTestOrderCommandHandler> logger)
    {
        _suiteRepository = suiteRepository;
        _proposalRepository = proposalRepository;
        _apiTestOrderService = apiTestOrderService;
        _logger = logger;
    }

    public async Task HandleAsync(ApproveApiTestOrderCommand command, CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();

        if (command.ProposalId == Guid.Empty)
        {
            throw new ValidationException("ProposalId là bắt buộc.");
        }

        var loadSuiteSw = Stopwatch.StartNew();
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == command.TestSuiteId));
        loadSuiteSw.Stop();
        _logger.LogInformation(
            "api_test_order.approve.load_suite completed. TestSuiteId={TestSuiteId}, ProposalId={ProposalId}, DurationMs={DurationMs}",
            command.TestSuiteId,
            command.ProposalId,
            loadSuiteSw.ElapsedMilliseconds);

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{command.TestSuiteId}'.");
        }

        EnsureOwnership(suite, command.CurrentUserId);

        var loadProposalSw = Stopwatch.StartNew();
        var proposal = await _proposalRepository.FirstOrDefaultAsync(
            _proposalRepository.GetQueryableSet()
                .Where(x => x.Id == command.ProposalId && x.TestSuiteId == command.TestSuiteId));
        loadProposalSw.Stop();
        _logger.LogInformation(
            "api_test_order.approve.load_proposal completed. TestSuiteId={TestSuiteId}, ProposalId={ProposalId}, DurationMs={DurationMs}",
            command.TestSuiteId,
            command.ProposalId,
            loadProposalSw.ElapsedMilliseconds);

        if (proposal == null)
        {
            throw new NotFoundException($"Không tìm thấy proposal với mã '{command.ProposalId}'.");
        }

        if (IsIdempotentApprovedProposal(proposal))
        {
            command.Result = ApiTestOrderModelMapper.ToModel(proposal, _apiTestOrderService);
            totalSw.Stop();
            _logger.LogInformation(
                "api_test_order.approve.completed_idempotent. TestSuiteId={TestSuiteId}, ProposalId={ProposalId}, DurationMs={DurationMs}",
                command.TestSuiteId,
                command.ProposalId,
                totalSw.ElapsedMilliseconds);
            return;
        }

        EnsurePendingProposal(proposal);
        _proposalRepository.SetRowVersion(proposal, ApiTestOrderModelMapper.ParseRowVersion(command.RowVersion));

        var userModifiedOrder = _apiTestOrderService.DeserializeOrderJson(proposal.UserModifiedOrder);
        var proposedOrder = _apiTestOrderService.DeserializeOrderJson(proposal.ProposedOrder);
        var finalOrder = userModifiedOrder.Count > 0 ? userModifiedOrder : proposedOrder;

        if (finalOrder.Count == 0)
        {
            throw new ValidationException("Không thể approve proposal vì thứ tự endpoint đang rỗng.");
        }

        var hasUserReorder = userModifiedOrder.Count > 0;
        var reviewedAt = DateTimeOffset.UtcNow;

        proposal.Status = hasUserReorder
            ? ProposalStatus.ModifiedAndApproved
            : ProposalStatus.Approved;
        proposal.ReviewedById = command.CurrentUserId;
        proposal.ReviewedAt = reviewedAt;
        proposal.ReviewNotes = string.IsNullOrWhiteSpace(command.ReviewNotes) ? proposal.ReviewNotes : command.ReviewNotes.Trim();
        proposal.AppliedOrder = _apiTestOrderService.SerializeOrderJson(finalOrder);
        proposal.AppliedAt = reviewedAt;
        proposal.RowVersion = Guid.NewGuid().ToByteArray();

        suite.ApprovalStatus = hasUserReorder
            ? ApprovalStatus.ModifiedAndApproved
            : ApprovalStatus.Approved;
        suite.ApprovedById = command.CurrentUserId;
        suite.ApprovedAt = reviewedAt;
        suite.LastModifiedById = command.CurrentUserId;
        suite.RowVersion = Guid.NewGuid().ToByteArray();

        try
        {
            var saveSw = Stopwatch.StartNew();
            await _proposalRepository.UnitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                await _proposalRepository.UpdateAsync(proposal, ct);
                await _suiteRepository.UpdateAsync(suite, ct);
                await _proposalRepository.UnitOfWork.SaveChangesAsync(ct);
            }, cancellationToken: cancellationToken);
            saveSw.Stop();
            _logger.LogInformation(
                "api_test_order.approve.save completed. TestSuiteId={TestSuiteId}, ProposalId={ProposalId}, Status={Status}, DurationMs={DurationMs}",
                command.TestSuiteId,
                command.ProposalId,
                proposal.Status,
                saveSw.ElapsedMilliseconds);
        }
        catch (Exception ex) when (_proposalRepository.IsDbUpdateConcurrencyException(ex))
        {
            throw new ConflictException(
                TestOrderReasonCodes.ConcurrencyConflict,
                "Dữ liệu proposal đã thay đổi bởi thao tác khác. Vui lòng tải lại và thử lại.");
        }

        command.Result = ApiTestOrderModelMapper.ToModel(proposal, _apiTestOrderService);
        totalSw.Stop();
        _logger.LogInformation(
            "Approved test order proposal. TestSuiteId={TestSuiteId}, ProposalId={ProposalId}, ProposalNumber={ProposalNumber}, Status={Status}, ActorUserId={ActorUserId}, DurationMs={DurationMs}",
            command.TestSuiteId,
            command.ProposalId,
            proposal.ProposalNumber,
            proposal.Status,
            command.CurrentUserId,
            totalSw.ElapsedMilliseconds);
    }

    private static void EnsureOwnership(TestSuite suite, Guid currentUserId)
    {
        if (suite.CreatedById != currentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }
    }

    private static bool IsIdempotentApprovedProposal(TestOrderProposal proposal)
    {
        return (proposal.Status == ProposalStatus.Approved
                || proposal.Status == ProposalStatus.ModifiedAndApproved
                || proposal.Status == ProposalStatus.Applied)
            && !string.IsNullOrWhiteSpace(proposal.AppliedOrder);
    }

    private static void EnsurePendingProposal(TestOrderProposal proposal)
    {
        if (proposal.Status != ProposalStatus.Pending)
        {
            throw new ValidationException("Chỉ có thể approve proposal ở trạng thái Pending.");
        }
    }
}
