using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestExecution.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.LlmAssistant.Commands;
using ClassifiedAds.Modules.LlmAssistant.Models;
using ClassifiedAds.Modules.LlmAssistant.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.LlmAssistant;

public class ExplainTestFailureCommandHandlerTests
{
    private readonly Mock<ITestFailureReadGatewayService> _failureReadGatewayServiceMock;
    private readonly Mock<IApiEndpointMetadataService> _endpointMetadataServiceMock;
    private readonly Mock<ILlmFailureExplainer> _explainerMock;
    private readonly ExplainTestFailureCommandHandler _handler;

    public ExplainTestFailureCommandHandlerTests()
    {
        _failureReadGatewayServiceMock = new Mock<ITestFailureReadGatewayService>();
        _endpointMetadataServiceMock = new Mock<IApiEndpointMetadataService>();
        _explainerMock = new Mock<ILlmFailureExplainer>();

        _handler = new ExplainTestFailureCommandHandler(
            _failureReadGatewayServiceMock.Object,
            _endpointMetadataServiceMock.Object,
            _explainerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_MetadataAvailable_ShouldLoadMetadataAndReturnResult()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var apiSpecId = Guid.NewGuid();
        var command = CreateCommand(currentUserId);
        var context = CreateContext(currentUserId, endpointId, apiSpecId);
        var metadata = new ApiEndpointMetadataDto
        {
            EndpointId = endpointId,
            HttpMethod = "POST",
            Path = "/api/orders",
        };
        var expected = CreateResult(context, "live");

        _failureReadGatewayServiceMock.Setup(x => x.GetFailureExplanationContextAsync(
                command.TestSuiteId,
                command.RunId,
                command.TestCaseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                apiSpecId,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(endpointId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { metadata });
        _explainerMock.Setup(x => x.ExplainAsync(context, metadata, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task HandleAsync_MetadataMissing_ShouldStillWork()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var apiSpecId = Guid.NewGuid();
        var command = CreateCommand(currentUserId);
        var context = CreateContext(currentUserId, endpointId, apiSpecId);
        var expected = CreateResult(context, "cache");

        _failureReadGatewayServiceMock.Setup(x => x.GetFailureExplanationContextAsync(
                command.TestSuiteId,
                command.RunId,
                command.TestCaseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                apiSpecId,
                It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(endpointId)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<ApiEndpointMetadataDto>());
        _explainerMock.Setup(x => x.ExplainAsync(context, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task HandleAsync_MetadataOptionalPath_ShouldSkipMetadataLookupAndInvokeExplainer()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var command = CreateCommand(currentUserId);
        var context = CreateContext(currentUserId, endpointId: Guid.NewGuid(), apiSpecId: null);
        context.Definition.EndpointId = null;
        var expected = CreateResult(context, "live");

        _failureReadGatewayServiceMock.Setup(x => x.GetFailureExplanationContextAsync(
                command.TestSuiteId,
                command.RunId,
                command.TestCaseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        _explainerMock.Setup(x => x.ExplainAsync(context, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        await _handler.HandleAsync(command);

        // Assert
        command.Result.Should().BeSameAs(expected);
        _endpointMetadataServiceMock.Verify(x => x.GetEndpointMetadataAsync(
            It.IsAny<Guid>(),
            It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _explainerMock.Verify(x => x.ExplainAsync(context, null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_OwnerMismatch_ShouldThrowValidationException()
    {
        // Arrange
        var command = CreateCommand(Guid.NewGuid());
        var context = CreateContext(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        _failureReadGatewayServiceMock.Setup(x => x.GetFailureExplanationContextAsync(
                command.TestSuiteId,
                command.RunId,
                command.TestCaseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _endpointMetadataServiceMock.Verify(x => x.GetEndpointMetadataAsync(
            It.IsAny<Guid>(),
            It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _explainerMock.Verify(x => x.ExplainAsync(
            It.IsAny<TestFailureExplanationContextDto>(),
            It.IsAny<ApiEndpointMetadataDto>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ExplainTestFailureCommand CreateCommand(Guid currentUserId)
    {
        return new ExplainTestFailureCommand
        {
            TestSuiteId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            TestCaseId = Guid.NewGuid(),
            CurrentUserId = currentUserId,
        };
    }

    private static TestFailureExplanationContextDto CreateContext(Guid ownerId, Guid endpointId, Guid? apiSpecId)
    {
        var testSuiteId = Guid.NewGuid();
        var testRunId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();

        return new TestFailureExplanationContextDto
        {
            TestSuiteId = testSuiteId,
            ProjectId = Guid.NewGuid(),
            ApiSpecId = apiSpecId,
            CreatedById = ownerId,
            TestRunId = testRunId,
            RunNumber = 8,
            TriggeredById = ownerId,
            ResolvedEnvironmentName = "Staging",
            ExecutedAt = DateTimeOffset.UtcNow,
            Definition = new FailureExplanationDefinitionDto
            {
                TestCaseId = testCaseId,
                EndpointId = endpointId,
                Name = "Create order",
                Description = "Should create order",
                TestType = "HappyPath",
                OrderIndex = 1,
            },
            ActualResult = new FailureExplanationActualResultDto
            {
                Status = "Failed",
                HttpStatusCode = 500,
                DurationMs = 220,
                ResolvedUrl = "/api/orders",
            },
        };
    }

    private static FailureExplanationModel CreateResult(TestFailureExplanationContextDto context, string source)
    {
        return new FailureExplanationModel
        {
            TestSuiteId = context.TestSuiteId,
            TestRunId = context.TestRunId,
            TestCaseId = context.Definition.TestCaseId,
            EndpointId = context.Definition.EndpointId,
            SummaryVi = "Loi do response status khong dung.",
            PossibleCauses = new List<string> { "Service bi loi." },
            SuggestedNextActions = new List<string> { "Kiem tra log backend." },
            Confidence = "High",
            Source = source,
            Provider = "N8n",
            Model = "gpt-4.1-mini",
            TokensUsed = 111,
            LatencyMs = 1234,
            GeneratedAt = DateTimeOffset.UtcNow,
            FailureCodes = new List<string> { "STATUS_CODE_MISMATCH" },
        };
    }
}
