using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Services;

public class ApiTestOrderGateService : IApiTestOrderGateService
{
    private static readonly ProposalStatus[] ActiveStatuses =
    {
        ProposalStatus.Approved,
        ProposalStatus.ModifiedAndApproved,
        ProposalStatus.Applied,
    };

    private readonly IRepository<TestSuite, Guid> _suiteRepository;
    private readonly IRepository<TestOrderProposal, Guid> _proposalRepository;
    private readonly IApiTestOrderService _orderService;

    public ApiTestOrderGateService(
        IRepository<TestSuite, Guid> suiteRepository,
        IRepository<TestOrderProposal, Guid> proposalRepository,
        IApiTestOrderService orderService)
    {
        _suiteRepository = suiteRepository;
        _proposalRepository = proposalRepository;
        _orderService = orderService;
    }

    public async Task<IReadOnlyList<ApiOrderItemModel>> RequireApprovedOrderAsync(
        Guid testSuiteId,
        CancellationToken cancellationToken = default)
    {
        var gateStatus = await GetGateStatusAsync(testSuiteId, cancellationToken);
        if (!gateStatus.IsGatePassed)
        {
            throw new ConflictException(
                TestOrderReasonCodes.OrderConfirmationRequired,
                "Chưa có API order được xác nhận. Vui lòng review/approve thứ tự API trước khi tạo test cases.");
        }

        var activeProposal = await FindActiveProposalAsync(testSuiteId, cancellationToken);
        if (activeProposal == null)
        {
            throw new ConflictException(
                TestOrderReasonCodes.OrderConfirmationRequired,
                "Chưa có API order được xác nhận. Vui lòng review/approve thứ tự API trước khi tạo test cases.");
        }

        return _orderService.DeserializeOrderJson(activeProposal.AppliedOrder);
    }

    public async Task<ApiTestOrderGateStatusModel> GetGateStatusAsync(
        Guid testSuiteId,
        CancellationToken cancellationToken = default)
    {
        var suite = await _suiteRepository.FirstOrDefaultAsync(
            _suiteRepository.GetQueryableSet().Where(x => x.Id == testSuiteId));

        if (suite == null)
        {
            throw new NotFoundException($"Không tìm thấy test suite với mã '{testSuiteId}'.");
        }

        var activeProposal = await FindActiveProposalAsync(testSuiteId, cancellationToken);
        if (activeProposal == null)
        {
            return new ApiTestOrderGateStatusModel
            {
                TestSuiteId = testSuiteId,
                IsGatePassed = false,
                ReasonCode = TestOrderReasonCodes.OrderConfirmationRequired,
                ActiveProposalId = null,
                ActiveProposalStatus = null,
                OrderSize = 0,
                EvaluatedAt = DateTimeOffset.UtcNow,
            };
        }

        var appliedOrder = _orderService.DeserializeOrderJson(activeProposal.AppliedOrder);
        var isGatePassed = appliedOrder.Count > 0;

        return new ApiTestOrderGateStatusModel
        {
            TestSuiteId = testSuiteId,
            IsGatePassed = isGatePassed,
            ReasonCode = isGatePassed ? null : TestOrderReasonCodes.OrderConfirmationRequired,
            ActiveProposalId = activeProposal.Id,
            ActiveProposalStatus = activeProposal.Status,
            OrderSize = appliedOrder.Count,
            EvaluatedAt = DateTimeOffset.UtcNow,
        };
    }

    private async Task<TestOrderProposal> FindActiveProposalAsync(Guid testSuiteId, CancellationToken cancellationToken)
    {
        return await _proposalRepository.FirstOrDefaultAsync(
            _proposalRepository.GetQueryableSet()
                .Where(x => x.TestSuiteId == testSuiteId
                    && ActiveStatuses.Contains(x.Status)
                    && x.AppliedOrder != null)
                .OrderByDescending(x => x.ProposalNumber));
    }
}
