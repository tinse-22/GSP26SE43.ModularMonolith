using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Controllers;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class SrsTraceabilityControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<SrsTraceabilityController>> _loggerMock;
    private readonly Mock<ICommandHandler<CreateTraceabilityLinkCommand>> _createLinkHandlerMock;
    private readonly Mock<ICommandHandler<DeleteTraceabilityLinkCommand>> _deleteLinkHandlerMock;
    private readonly Mock<IQueryHandler<GetSrsTraceabilityQuery, TraceabilityMatrix>> _getTraceabilityHandlerMock;
    private readonly SrsTraceabilityController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public SrsTraceabilityControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<SrsTraceabilityController>>();
        _createLinkHandlerMock = new Mock<ICommandHandler<CreateTraceabilityLinkCommand>>();
        _deleteLinkHandlerMock = new Mock<ICommandHandler<DeleteTraceabilityLinkCommand>>();
        _getTraceabilityHandlerMock = new Mock<IQueryHandler<GetSrsTraceabilityQuery, TraceabilityMatrix>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var services = new Dictionary<Type, object>
        {
            [typeof(ICommandHandler<CreateTraceabilityLinkCommand>)] = _createLinkHandlerMock.Object,
            [typeof(ICommandHandler<DeleteTraceabilityLinkCommand>)] = _deleteLinkHandlerMock.Object,
            [typeof(IQueryHandler<GetSrsTraceabilityQuery, TraceabilityMatrix>)] = _getTraceabilityHandlerMock.Object,
        };

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(It.IsAny<Type>()))
            .Returns((Type serviceType) => services.TryGetValue(serviceType, out var service) ? service : null);

        _controller = new SrsTraceabilityController(new Dispatcher(serviceProviderMock.Object), _currentUserMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GetTraceability_Should_ReturnOkWithMatrix()
    {
        var suiteId = Guid.NewGuid();
        var matrix = new TraceabilityMatrix
        {
            TestSuiteId = suiteId,
            TotalRequirements = 5,
            CoveredRequirements = 4,
            CoveragePercent = 80,
            Requirements =
            {
                new TraceabilityRequirementRow
                {
                    RequirementId = Guid.NewGuid(),
                    RequirementCode = "REQ-1",
                    Title = "Login success",
                    IsCovered = true,
                },
            },
        };

        _getTraceabilityHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsTraceabilityQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matrix);

        var result = await _controller.GetTraceability(Guid.NewGuid(), suiteId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<TraceabilityMatrix>().Subject;
        payload.TestSuiteId.Should().Be(suiteId);
        payload.CoveragePercent.Should().Be(80);
    }

    [Fact]
    public async Task GetTraceability_Should_MapIdsAndOptionalTestRun()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var testRunId = Guid.NewGuid();
        GetSrsTraceabilityQuery capturedQuery = null!;

        _getTraceabilityHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsTraceabilityQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSrsTraceabilityQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new TraceabilityMatrix());

        await _controller.GetTraceability(projectId, suiteId, testRunId);

        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.TestSuiteId.Should().Be(suiteId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
        capturedQuery.TestRunId.Should().Be(testRunId);
    }

    [Fact]
    public async Task GetTraceability_Should_DefaultTestRunToNull()
    {
        GetSrsTraceabilityQuery capturedQuery = null!;

        _getTraceabilityHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsTraceabilityQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetSrsTraceabilityQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new TraceabilityMatrix());

        await _controller.GetTraceability(Guid.NewGuid(), Guid.NewGuid());

        capturedQuery.TestRunId.Should().BeNull();
    }

    [Fact]
    public async Task GetTraceability_Should_PropagateNotFoundException()
    {
        _getTraceabilityHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetSrsTraceabilityQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Traceability matrix not found"));

        var act = () => _controller.GetTraceability(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Traceability matrix not found*");
    }

    [Fact]
    public async Task CreateLink_Should_ReturnCreatedWithLink()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var link = new TraceabilityLinkModel
        {
            Id = Guid.NewGuid(),
            TestCaseId = Guid.NewGuid(),
            TestCaseName = "Verify checkout total",
            SrsRequirementId = Guid.NewGuid(),
            RequirementCode = "REQ-12",
        };

        _createLinkHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateTraceabilityLinkCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTraceabilityLinkCommand, CancellationToken>((command, _) => command.Result = link)
            .Returns(Task.CompletedTask);

        var result = await _controller.CreateLink(projectId, suiteId, new CreateTraceabilityLinkRequest { TestCaseId = link.TestCaseId, SrsRequirementId = link.SrsRequirementId });

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/projects/{projectId}/test-suites/{suiteId}/traceability");
        createdResult.Value.Should().BeOfType<TraceabilityLinkModel>().Subject.RequirementCode.Should().Be("REQ-12");
    }

    [Fact]
    public async Task CreateLink_Should_MapBodyAndIdentifiers()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var requirementId = Guid.NewGuid();
        CreateTraceabilityLinkCommand capturedCommand = null!;

        _createLinkHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateTraceabilityLinkCommand>(), It.IsAny<CancellationToken>()))
            .Callback<CreateTraceabilityLinkCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = new TraceabilityLinkModel();
            })
            .Returns(Task.CompletedTask);

        await _controller.CreateLink(projectId, suiteId, new CreateTraceabilityLinkRequest { TestCaseId = testCaseId, SrsRequirementId = requirementId });

        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.TestCaseId.Should().Be(testCaseId);
        capturedCommand.SrsRequirementId.Should().Be(requirementId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task CreateLink_Should_PropagateValidationException()
    {
        _createLinkHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<CreateTraceabilityLinkCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Traceability link already exists"));

        var act = () => _controller.CreateLink(Guid.NewGuid(), Guid.NewGuid(), new CreateTraceabilityLinkRequest());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Traceability link already exists*");
    }

    [Fact]
    public async Task DeleteLink_Should_ReturnNoContent()
    {
        _deleteLinkHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteTraceabilityLinkCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.DeleteLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteLink_Should_MapIdentifiersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var linkId = Guid.NewGuid();
        DeleteTraceabilityLinkCommand capturedCommand = null!;

        _deleteLinkHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteTraceabilityLinkCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteTraceabilityLinkCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.DeleteLink(projectId, suiteId, linkId);

        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.LinkId.Should().Be(linkId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task DeleteLink_Should_PropagateNotFoundException()
    {
        _deleteLinkHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteTraceabilityLinkCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Traceability link not found"));

        var act = () => _controller.DeleteLink(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Traceability link not found*");
    }
}
