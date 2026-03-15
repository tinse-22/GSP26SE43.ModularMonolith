using ClassifiedAds.Contracts.TestExecution.DTOs;
using ClassifiedAds.Contracts.TestExecution.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.LlmAssistant.Models;
using ClassifiedAds.Modules.LlmAssistant.Queries;
using ClassifiedAds.Modules.LlmAssistant.Services;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.LlmAssistant;

public class GetFailureExplanationQueryHandlerTests
{
    private readonly Mock<ITestFailureReadGatewayService> _failureReadGatewayServiceMock;
    private readonly Mock<ILlmFailureExplainer> _explainerMock;
    private readonly GetFailureExplanationQueryHandler _handler;

    public GetFailureExplanationQueryHandlerTests()
    {
        _failureReadGatewayServiceMock = new Mock<ITestFailureReadGatewayService>();
        _explainerMock = new Mock<ILlmFailureExplainer>();

        _handler = new GetFailureExplanationQueryHandler(
            _failureReadGatewayServiceMock.Object,
            _explainerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_CacheMiss_ShouldThrowNotFoundExceptionWithPrefix()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var query = CreateQuery(currentUserId);
        var context = CreateContext(currentUserId);

        _failureReadGatewayServiceMock.Setup(x => x.GetFailureExplanationContextAsync(
                query.TestSuiteId,
                query.RunId,
                query.TestCaseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        _explainerMock.Setup(x => x.GetCachedAsync(context, It.IsAny<CancellationToken>()))
            .ReturnsAsync((FailureExplanationModel)null);

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.Message.Should().StartWith("FAILURE_EXPLANATION_NOT_FOUND:");
    }

    [Fact]
    public async Task HandleAsync_CacheHit_ShouldReturnModel()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var query = CreateQuery(currentUserId);
        var context = CreateContext(currentUserId);
        var expected = new FailureExplanationModel
        {
            TestSuiteId = context.TestSuiteId,
            TestRunId = context.TestRunId,
            TestCaseId = context.Definition.TestCaseId,
            EndpointId = context.Definition.EndpointId,
            SummaryVi = "Cached explanation",
            PossibleCauses = new[] { "Cached cause" },
            SuggestedNextActions = new[] { "Cached action" },
            Confidence = "Medium",
            Source = "cache",
            Provider = "N8n",
            Model = "gpt-4.1-mini",
            TokensUsed = 55,
            LatencyMs = 0,
            GeneratedAt = DateTimeOffset.UtcNow,
            FailureCodes = new[] { "STATUS_CODE_MISMATCH" },
        };

        _failureReadGatewayServiceMock.Setup(x => x.GetFailureExplanationContextAsync(
                query.TestSuiteId,
                query.RunId,
                query.TestCaseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);
        _explainerMock.Setup(x => x.GetCachedAsync(context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task HandleAsync_OwnerMismatch_ShouldThrowValidationException()
    {
        // Arrange
        var query = CreateQuery(Guid.NewGuid());
        var context = CreateContext(Guid.NewGuid());

        _failureReadGatewayServiceMock.Setup(x => x.GetFailureExplanationContextAsync(
                query.TestSuiteId,
                query.RunId,
                query.TestCaseId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _explainerMock.Verify(x => x.GetCachedAsync(It.IsAny<TestFailureExplanationContextDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GetFailureExplanationQuery CreateQuery(Guid currentUserId)
    {
        return new GetFailureExplanationQuery
        {
            TestSuiteId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            TestCaseId = Guid.NewGuid(),
            CurrentUserId = currentUserId,
        };
    }

    private static TestFailureExplanationContextDto CreateContext(Guid ownerId)
    {
        var testCaseId = Guid.NewGuid();

        return new TestFailureExplanationContextDto
        {
            TestSuiteId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            ApiSpecId = Guid.NewGuid(),
            CreatedById = ownerId,
            TestRunId = Guid.NewGuid(),
            RunNumber = 12,
            TriggeredById = ownerId,
            ResolvedEnvironmentName = "QA",
            ExecutedAt = DateTimeOffset.UtcNow,
            Definition = new FailureExplanationDefinitionDto
            {
                TestCaseId = testCaseId,
                EndpointId = Guid.NewGuid(),
                Name = "Create order",
                Description = "Should create order",
                TestType = "HappyPath",
                OrderIndex = 1,
            },
            ActualResult = new FailureExplanationActualResultDto
            {
                Status = "Failed",
                HttpStatusCode = 500,
                DurationMs = 100,
                ResolvedUrl = "/api/orders",
            },
        };
    }
}
