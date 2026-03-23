using ClassifiedAds.Application;
using ClassifiedAds.Contracts.TestExecution.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.LlmAssistant.Models;
using ClassifiedAds.Modules.LlmAssistant.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.LlmAssistant.Queries;

public class GetFailureExplanationQuery : IQuery<FailureExplanationModel>
{
    public Guid TestSuiteId { get; set; }

    public Guid RunId { get; set; }

    public Guid TestCaseId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class GetFailureExplanationQueryHandler : IQueryHandler<GetFailureExplanationQuery, FailureExplanationModel>
{
    private readonly ITestFailureReadGatewayService _failureReadGatewayService;
    private readonly ILlmFailureExplainer _explainer;

    public GetFailureExplanationQueryHandler(
        ITestFailureReadGatewayService failureReadGatewayService,
        ILlmFailureExplainer explainer)
    {
        _failureReadGatewayService = failureReadGatewayService;
        _explainer = explainer;
    }

    public async Task<FailureExplanationModel> HandleAsync(
        GetFailureExplanationQuery query,
        CancellationToken cancellationToken = default)
    {
        var context = await _failureReadGatewayService.GetFailureExplanationContextAsync(
            query.TestSuiteId,
            query.RunId,
            query.TestCaseId,
            cancellationToken);

        if (context.CreatedById != query.CurrentUserId)
        {
            throw new ValidationException("Bạn không có quyền thao tác test suite này.");
        }

        var cached = await _explainer.GetCachedAsync(context, cancellationToken);
        if (cached == null)
        {
            throw new NotFoundException(
                $"FAILURE_EXPLANATION_NOT_FOUND: Không tìm thấy cached explanation cho test case '{query.TestCaseId}'.");
        }

        return cached;
    }
}
