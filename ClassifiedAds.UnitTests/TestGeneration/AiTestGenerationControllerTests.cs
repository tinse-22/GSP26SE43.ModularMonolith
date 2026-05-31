using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Controllers;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class AiTestGenerationControllerTests
{
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<TestCasesController>> _loggerMock;
    private readonly Mock<ICommandHandler<GenerateHappyPathTestCasesCommand>> _happyPathHandlerMock;
    private readonly Mock<ICommandHandler<GenerateBoundaryNegativeTestCasesCommand>> _boundaryHandlerMock;
    private readonly Mock<ICommandHandler<GenerateTestCasesCommand>> _queueHandlerMock;

    public AiTestGenerationControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<TestCasesController>>();
        _happyPathHandlerMock = new Mock<ICommandHandler<GenerateHappyPathTestCasesCommand>>();
        _boundaryHandlerMock = new Mock<ICommandHandler<GenerateBoundaryNegativeTestCasesCommand>>();
        _queueHandlerMock = new Mock<ICommandHandler<GenerateTestCasesCommand>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);
    }

    [Fact]
    public async Task GenerateHappyPath_Should_ReturnCreatedWithResult_WhenWorkflowRunsInline()
    {
        var controller = CreateController(useQueuedWorkflow: false);
        var suiteId = Guid.NewGuid();
        var request = new GenerateHappyPathTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
            ForceRegenerate = true,
        };

        _happyPathHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateHappyPathTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateHappyPathTestCasesCommand, CancellationToken>((command, _) =>
            {
                command.Result = CreateHappyPathResult(suiteId, totalGenerated: 3);
            })
            .Returns(Task.CompletedTask);

        var result = await controller.GenerateHappyPath(suiteId, request);

        var created = result.Result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/test-suites/{suiteId}/test-cases");
        created.Value.Should().BeOfType<GenerateHappyPathResultModel>().Subject.TotalGenerated.Should().Be(3);
    }

    [Fact]
    public async Task GenerateHappyPath_Should_MapSuiteUserSpecificationAndForceRegenerate()
    {
        var controller = CreateController(useQueuedWorkflow: false);
        var suiteId = Guid.NewGuid();
        var request = new GenerateHappyPathTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
            ForceRegenerate = false,
        };
        GenerateHappyPathTestCasesCommand captured = null!;

        _happyPathHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateHappyPathTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateHappyPathTestCasesCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.Result = CreateHappyPathResult(suiteId, totalGenerated: 1);
            })
            .Returns(Task.CompletedTask);

        await controller.GenerateHappyPath(suiteId, request);

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.SpecificationId.Should().Be(request.SpecificationId);
        captured.ForceRegenerate.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateHappyPath_Should_ReturnCommandResultPayload()
    {
        var controller = CreateController(useQueuedWorkflow: false);
        var suiteId = Guid.NewGuid();
        var expected = CreateHappyPathResult(suiteId, totalGenerated: 4);
        expected.EndpointsCovered = 2;
        expected.LlmModel = "gpt-4.1-mini";

        _happyPathHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateHappyPathTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateHappyPathTestCasesCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await controller.GenerateHappyPath(suiteId, new GenerateHappyPathTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
        });

        var payload = result.Result.Should().BeOfType<CreatedResult>().Subject.Value
            .Should().BeOfType<GenerateHappyPathResultModel>().Subject;
        payload.TotalGenerated.Should().Be(4);
        payload.EndpointsCovered.Should().Be(2);
        payload.LlmModel.Should().Be("gpt-4.1-mini");
    }

    [Fact]
    public async Task GenerateHappyPath_Should_ReturnAcceptedAndJobId_WhenQueuedWorkflowEnabled()
    {
        var controller = CreateController(useQueuedWorkflow: true);
        var suiteId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        _queueHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateTestCasesCommand, CancellationToken>((command, _) => command.JobId = jobId)
            .Returns(Task.CompletedTask);

        var result = await controller.GenerateHappyPath(suiteId, new GenerateHappyPathTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
            ForceRegenerate = true,
        });

        var accepted = result.Result.Should().BeOfType<ObjectResult>().Subject;
        accepted.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        var payload = accepted.Value.Should().BeOfType<GenerateTestsAcceptedResponse>().Subject;
        payload.JobId.Should().Be(jobId);
        payload.TestSuiteId.Should().Be(suiteId);
        payload.Mode.Should().Be("callback");
    }

    [Fact]
    public async Task GenerateHappyPath_Should_QueueUnifiedCommandWithSuiteAndCurrentUser()
    {
        var controller = CreateController(useQueuedWorkflow: true);
        var suiteId = Guid.NewGuid();
        GenerateTestCasesCommand captured = null!;

        _queueHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateTestCasesCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.JobId = Guid.NewGuid();
            })
            .Returns(Task.CompletedTask);

        await controller.GenerateHappyPath(suiteId, new GenerateHappyPathTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
            ForceRegenerate = false,
        });

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GenerateBoundaryNegative_Should_ReturnCreatedWithResult_WhenWorkflowRunsInline()
    {
        var controller = CreateController(useQueuedWorkflow: false);
        var suiteId = Guid.NewGuid();
        var request = new GenerateBoundaryNegativeTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
            ForceRegenerate = true,
            IncludePathMutations = true,
            IncludeBodyMutations = true,
            IncludeLlmSuggestions = true,
        };

        _boundaryHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateBoundaryNegativeTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateBoundaryNegativeTestCasesCommand, CancellationToken>((command, _) =>
            {
                command.Result = CreateBoundaryResult(suiteId, totalGenerated: 5);
            })
            .Returns(Task.CompletedTask);

        var result = await controller.GenerateBoundaryNegative(suiteId, request);

        var created = result.Result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/test-suites/{suiteId}/test-cases");
        created.Value.Should().BeOfType<GenerateBoundaryNegativeResultModel>().Subject.TotalGenerated.Should().Be(5);
    }

    [Fact]
    public async Task GenerateBoundaryNegative_Should_MapSuiteUserSpecificationAndMutationFlags()
    {
        var controller = CreateController(useQueuedWorkflow: false);
        var suiteId = Guid.NewGuid();
        var request = new GenerateBoundaryNegativeTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
            ForceRegenerate = false,
            IncludePathMutations = true,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = true,
        };
        GenerateBoundaryNegativeTestCasesCommand captured = null!;

        _boundaryHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateBoundaryNegativeTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateBoundaryNegativeTestCasesCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.Result = CreateBoundaryResult(suiteId, totalGenerated: 2);
            })
            .Returns(Task.CompletedTask);

        await controller.GenerateBoundaryNegative(suiteId, request);

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.SpecificationId.Should().Be(request.SpecificationId);
        captured.ForceRegenerate.Should().BeFalse();
        captured.IncludePathMutations.Should().BeTrue();
        captured.IncludeBodyMutations.Should().BeFalse();
        captured.IncludeLlmSuggestions.Should().BeTrue();
    }

    [Fact]
    public async Task GenerateBoundaryNegative_Should_ReturnMutationCountsAndModelFromResult()
    {
        var controller = CreateController(useQueuedWorkflow: false);
        var suiteId = Guid.NewGuid();
        var expected = CreateBoundaryResult(suiteId, totalGenerated: 6);
        expected.PathMutationCount = 2;
        expected.BodyMutationCount = 1;
        expected.LlmSuggestionCount = 3;
        expected.EndpointsCovered = 4;
        expected.LlmModel = "gpt-4.1";

        _boundaryHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateBoundaryNegativeTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateBoundaryNegativeTestCasesCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await controller.GenerateBoundaryNegative(suiteId, new GenerateBoundaryNegativeTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
            IncludePathMutations = true,
            IncludeBodyMutations = true,
            IncludeLlmSuggestions = true,
        });

        var payload = result.Result.Should().BeOfType<CreatedResult>().Subject.Value
            .Should().BeOfType<GenerateBoundaryNegativeResultModel>().Subject;
        payload.TotalGenerated.Should().Be(6);
        payload.PathMutationCount.Should().Be(2);
        payload.BodyMutationCount.Should().Be(1);
        payload.LlmSuggestionCount.Should().Be(3);
        payload.EndpointsCovered.Should().Be(4);
        payload.LlmModel.Should().Be("gpt-4.1");
    }

    [Fact]
    public async Task GenerateBoundaryNegative_Should_ReturnAcceptedAndJobId_WhenQueuedWorkflowEnabled()
    {
        var controller = CreateController(useQueuedWorkflow: true);
        var suiteId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        _queueHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateTestCasesCommand, CancellationToken>((command, _) => command.JobId = jobId)
            .Returns(Task.CompletedTask);

        var result = await controller.GenerateBoundaryNegative(suiteId, new GenerateBoundaryNegativeTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
            IncludePathMutations = true,
            IncludeBodyMutations = true,
            IncludeLlmSuggestions = true,
        });

        var accepted = result.Result.Should().BeOfType<ObjectResult>().Subject;
        accepted.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        var payload = accepted.Value.Should().BeOfType<GenerateTestsAcceptedResponse>().Subject;
        payload.JobId.Should().Be(jobId);
        payload.TestSuiteId.Should().Be(suiteId);
        payload.Mode.Should().Be("callback");
    }

    [Fact]
    public async Task GenerateBoundaryNegative_Should_QueueUnifiedCommandWithSuiteAndCurrentUser()
    {
        var controller = CreateController(useQueuedWorkflow: true);
        var suiteId = Guid.NewGuid();
        GenerateTestCasesCommand captured = null!;

        _queueHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<GenerateTestCasesCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.JobId = Guid.NewGuid();
            })
            .Returns(Task.CompletedTask);

        await controller.GenerateBoundaryNegative(suiteId, new GenerateBoundaryNegativeTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
            IncludePathMutations = false,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = true,
        });

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GenerateBoundaryNegative_Should_ThrowValidationException_WhenGenerationFails()
    {
        var controller = CreateController(useQueuedWorkflow: false);

        _boundaryHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GenerateBoundaryNegativeTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("At least one generation source must be enabled"));

        var act = () => controller.GenerateBoundaryNegative(Guid.NewGuid(), new GenerateBoundaryNegativeTestCasesRequest
        {
            SpecificationId = Guid.NewGuid(),
            IncludePathMutations = false,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = false,
        });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*At least one generation source must be enabled*");
    }

    private TestCasesController CreateController(bool useQueuedWorkflow)
    {
        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<GenerateHappyPathTestCasesCommand>))).Returns(_happyPathHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<GenerateBoundaryNegativeTestCasesCommand>))).Returns(_boundaryHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<GenerateTestCasesCommand>))).Returns(_queueHandlerMock.Object);

        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        var controller = new TestCasesController(
            dispatcher,
            _currentUserMock.Object,
            _loggerMock.Object,
            Options.Create(new N8nIntegrationOptions
            {
                UseDotnetIntegrationWorkflowForGeneration = useQueuedWorkflow,
            }));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };

        return controller;
    }

    private static GenerateHappyPathResultModel CreateHappyPathResult(Guid suiteId, int totalGenerated)
    {
        return new GenerateHappyPathResultModel
        {
            TestSuiteId = suiteId,
            TotalGenerated = totalGenerated,
            EndpointsCovered = Math.Max(1, totalGenerated - 1),
            LlmModel = "gpt-4.1-mini",
            TokensUsed = 1200,
            GeneratedAt = DateTimeOffset.UtcNow,
            TestCases = new List<GeneratedTestCaseSummary>
            {
                new()
                {
                    TestCaseId = Guid.NewGuid(),
                    EndpointId = Guid.NewGuid(),
                    Name = "Generated happy path",
                    HttpMethod = "POST",
                    Path = "/auth/login",
                    OrderIndex = 1,
                    VariableCount = 1,
                },
            },
        };
    }

    private static GenerateBoundaryNegativeResultModel CreateBoundaryResult(Guid suiteId, int totalGenerated)
    {
        return new GenerateBoundaryNegativeResultModel
        {
            TestSuiteId = suiteId,
            TotalGenerated = totalGenerated,
            PathMutationCount = 1,
            BodyMutationCount = 1,
            LlmSuggestionCount = Math.Max(1, totalGenerated - 2),
            EndpointsCovered = Math.Max(1, totalGenerated - 1),
            LlmModel = "gpt-4.1-mini",
            LlmTokensUsed = 1800,
            GeneratedAt = DateTimeOffset.UtcNow,
            TestCases = new List<GeneratedTestCaseSummary>
            {
                new()
                {
                    TestCaseId = Guid.NewGuid(),
                    EndpointId = Guid.NewGuid(),
                    Name = "Generated negative path",
                    HttpMethod = "POST",
                    Path = "/users",
                    OrderIndex = 2,
                    VariableCount = 0,
                },
            },
        };
    }
}
