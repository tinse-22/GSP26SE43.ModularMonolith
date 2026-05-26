using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Controllers;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class TestOrderGenerationStatusControllerTests
{
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IRepository<TestGenerationJob, Guid>> _jobRepositoryMock;
    private readonly Mock<ILogger<TestOrderController>> _loggerMock;
    private readonly Mock<IQueryHandler<GetGenerationJobStatusQuery, GenerationJobStatusDto>> _statusHandlerMock;
    private readonly TestOrderController _controller;

    public TestOrderGenerationStatusControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _jobRepositoryMock = new Mock<IRepository<TestGenerationJob, Guid>>();
        _loggerMock = new Mock<ILogger<TestOrderController>>();
        _statusHandlerMock = new Mock<IQueryHandler<GetGenerationJobStatusQuery, GenerationJobStatusDto>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetGenerationJobStatusQuery, GenerationJobStatusDto>))).Returns(_statusHandlerMock.Object);

        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new TestOrderController(
            dispatcher,
            _currentUserMock.Object,
            _jobRepositoryMock.Object,
            Options.Create(new N8nIntegrationOptions()),
            _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GetGenerationStatus_Should_ReturnOkWithStatusPayload()
    {
        var suiteId = Guid.NewGuid();
        var jobId = Guid.NewGuid();

        _statusHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetGenerationJobStatusQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerationJobStatusDto
            {
                JobId = jobId,
                TestSuiteId = suiteId,
                Status = "Queued",
                WebhookName = "generate-llm-suggestions",
            });

        var result = await _controller.GetGenerationStatus(suiteId, jobId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<GenerationJobStatusDto>().Subject.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task GetGenerationStatus_Should_MapSuiteJobAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        GetGenerationJobStatusQuery captured = null!;

        _statusHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetGenerationJobStatusQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetGenerationJobStatusQuery, CancellationToken>((query, _) => captured = query)
            .ReturnsAsync(new GenerationJobStatusDto
            {
                JobId = jobId,
                TestSuiteId = suiteId,
                Status = "Completed",
            });

        await _controller.GetGenerationStatus(suiteId, jobId);

        captured.Should().NotBeNull();
        captured.JobId.Should().Be(jobId);
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetGenerationStatus_Should_DefaultJobIdToEmpty_WhenNotProvided()
    {
        var suiteId = Guid.NewGuid();
        GetGenerationJobStatusQuery captured = null!;

        _statusHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetGenerationJobStatusQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetGenerationJobStatusQuery, CancellationToken>((query, _) => captured = query)
            .ReturnsAsync(new GenerationJobStatusDto
            {
                JobId = Guid.NewGuid(),
                TestSuiteId = suiteId,
                Status = "Triggering",
            });

        await _controller.GetGenerationStatus(suiteId, null);

        captured.Should().NotBeNull();
        captured.JobId.Should().Be(Guid.Empty);
        captured.TestSuiteId.Should().Be(suiteId);
    }

    [Fact]
    public async Task GetGenerationStatus_Should_ReturnTimingAndWebhookFields()
    {
        var suiteId = Guid.NewGuid();
        var queuedAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        _statusHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetGenerationJobStatusQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerationJobStatusDto
            {
                JobId = Guid.NewGuid(),
                TestSuiteId = suiteId,
                Status = "Completed",
                QueuedAt = queuedAt,
                CompletedAt = queuedAt.AddMinutes(2),
                TestCasesGenerated = 7,
                WebhookName = "generate-llm-suggestions",
            });

        var result = await _controller.GetGenerationStatus(suiteId, Guid.NewGuid());

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<GenerationJobStatusDto>().Subject;
        payload.TestCasesGenerated.Should().Be(7);
        payload.WebhookName.Should().Be("generate-llm-suggestions");
        payload.CompletedAt.Should().NotBeNull();
    }
}
