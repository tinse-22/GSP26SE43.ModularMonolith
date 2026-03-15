using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.TestExecution.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.LlmAssistant.Models;
using ClassifiedAds.Modules.LlmAssistant.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.LlmAssistant.Commands;

public class ExplainTestFailureCommand : ICommand
{
    public Guid TestSuiteId { get; set; }

    public Guid RunId { get; set; }

    public Guid TestCaseId { get; set; }

    public Guid CurrentUserId { get; set; }

    public FailureExplanationModel Result { get; set; }
}

public class ExplainTestFailureCommandHandler : ICommandHandler<ExplainTestFailureCommand>
{
    private readonly ITestFailureReadGatewayService _failureReadGatewayService;
    private readonly IApiEndpointMetadataService _endpointMetadataService;
    private readonly ILlmFailureExplainer _explainer;

    public ExplainTestFailureCommandHandler(
        ITestFailureReadGatewayService failureReadGatewayService,
        IApiEndpointMetadataService endpointMetadataService,
        ILlmFailureExplainer explainer)
    {
        _failureReadGatewayService = failureReadGatewayService;
        _endpointMetadataService = endpointMetadataService;
        _explainer = explainer;
    }

    public async Task HandleAsync(ExplainTestFailureCommand command, CancellationToken cancellationToken = default)
    {
        var context = await _failureReadGatewayService.GetFailureExplanationContextAsync(
            command.TestSuiteId,
            command.RunId,
            command.TestCaseId,
            cancellationToken);

        if (context.CreatedById != command.CurrentUserId)
        {
            throw new ValidationException("Ban khong co quyen thao tac test suite nay.");
        }

        ApiEndpointMetadataDto endpointMetadata = null;
        if (context.ApiSpecId.HasValue && context.Definition?.EndpointId.HasValue == true)
        {
            var metadata = await _endpointMetadataService.GetEndpointMetadataAsync(
                context.ApiSpecId.Value,
                new[] { context.Definition.EndpointId.Value },
                cancellationToken);

            endpointMetadata = metadata?.FirstOrDefault(x => x.EndpointId == context.Definition.EndpointId.Value);
        }

        command.Result = await _explainer.ExplainAsync(context, endpointMetadata, cancellationToken);
    }
}
