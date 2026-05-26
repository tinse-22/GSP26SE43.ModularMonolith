using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Controllers;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class TestOrderControllerTests
{
    private readonly Guid _currentUserId = Guid.NewGuid();
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<IRepository<TestGenerationJob, Guid>> _jobRepositoryMock;
    private readonly Mock<ILogger<TestOrderController>> _loggerMock;
    private readonly Mock<ICommandHandler<ProposeApiTestOrderCommand>> _proposeHandlerMock;
    private readonly Mock<IQueryHandler<GetLatestApiTestOrderProposalQuery, ApiTestOrderProposalModel>> _getLatestHandlerMock;
    private readonly Mock<ICommandHandler<ApproveApiTestOrderCommand>> _approveHandlerMock;
    private readonly Mock<ICommandHandler<RejectApiTestOrderCommand>> _rejectHandlerMock;
    private readonly Mock<ICommandHandler<ReorderApiTestOrderCommand>> _reorderHandlerMock;
    private readonly Mock<IQueryHandler<GetApiTestOrderGateStatusQuery, ApiTestOrderGateStatusModel>> _gateStatusHandlerMock;
    private readonly TestOrderController _controller;

    public TestOrderControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _jobRepositoryMock = new Mock<IRepository<TestGenerationJob, Guid>>();
        _loggerMock = new Mock<ILogger<TestOrderController>>();
        _proposeHandlerMock = new Mock<ICommandHandler<ProposeApiTestOrderCommand>>();
        _getLatestHandlerMock = new Mock<IQueryHandler<GetLatestApiTestOrderProposalQuery, ApiTestOrderProposalModel>>();
        _approveHandlerMock = new Mock<ICommandHandler<ApproveApiTestOrderCommand>>();
        _rejectHandlerMock = new Mock<ICommandHandler<RejectApiTestOrderCommand>>();
        _reorderHandlerMock = new Mock<ICommandHandler<ReorderApiTestOrderCommand>>();
        _gateStatusHandlerMock = new Mock<IQueryHandler<GetApiTestOrderGateStatusQuery, ApiTestOrderGateStatusModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<ProposeApiTestOrderCommand>))).Returns(_proposeHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetLatestApiTestOrderProposalQuery, ApiTestOrderProposalModel>))).Returns(_getLatestHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<ApproveApiTestOrderCommand>))).Returns(_approveHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<RejectApiTestOrderCommand>))).Returns(_rejectHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<ReorderApiTestOrderCommand>))).Returns(_reorderHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetApiTestOrderGateStatusQuery, ApiTestOrderGateStatusModel>))).Returns(_gateStatusHandlerMock.Object);

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
    public async Task Propose_Should_ReturnCreatedWithProposalPayload()
    {
        var suiteId = Guid.NewGuid();
        var proposal = CreateProposal(suiteId, ProposalStatus.Pending, ProposalSource.Ai);

        _proposeHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ProposeApiTestOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ProposeApiTestOrderCommand, CancellationToken>((command, _) => command.Result = proposal)
            .Returns(Task.CompletedTask);

        var result = await _controller.Propose(suiteId, new ProposeApiTestOrderRequest
        {
            SpecificationId = Guid.NewGuid(),
            SelectedEndpointIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            Source = ProposalSource.Ai,
            LlmModel = "gpt-4.1-mini",
            ReasoningNote = "Auth endpoint should run first",
        });

        var created = result.Result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/test-suites/{suiteId}/order-proposals/{proposal.ProposalId}");
        created.Value.Should().BeSameAs(proposal);
    }

    [Fact]
    public async Task Propose_Should_MapSuiteCurrentUserSpecificationEndpointsAndMetadata()
    {
        var suiteId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ProposeApiTestOrderCommand captured = null!;

        _proposeHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ProposeApiTestOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ProposeApiTestOrderCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.Result = CreateProposal(suiteId, ProposalStatus.Pending, ProposalSource.User);
            })
            .Returns(Task.CompletedTask);

        await _controller.Propose(suiteId, new ProposeApiTestOrderRequest
        {
            SpecificationId = specId,
            SelectedEndpointIds = endpointIds,
            Source = ProposalSource.User,
            LlmModel = "gpt-4.1",
            ReasoningNote = "Manual order refinement",
        });

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.SpecificationId.Should().Be(specId);
        captured.SelectedEndpointIds.Should().BeEquivalentTo(endpointIds);
        captured.Source.Should().Be(ProposalSource.User);
        captured.LlmModel.Should().Be("gpt-4.1");
        captured.ReasoningNote.Should().Be("Manual order refinement");
    }

    [Fact]
    public async Task GetLatest_Should_ReturnOkWithProposalPayload()
    {
        var suiteId = Guid.NewGuid();
        var proposal = CreateProposal(suiteId, ProposalStatus.ModifiedAndApproved, ProposalSource.System);

        _getLatestHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetLatestApiTestOrderProposalQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(proposal);

        var result = await _controller.GetLatest(suiteId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(proposal);
    }

    [Fact]
    public async Task GetLatest_Should_MapSuiteAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        GetLatestApiTestOrderProposalQuery captured = null!;

        _getLatestHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetLatestApiTestOrderProposalQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetLatestApiTestOrderProposalQuery, CancellationToken>((query, _) => captured = query)
            .ReturnsAsync(CreateProposal(suiteId, ProposalStatus.Pending, ProposalSource.Ai));

        await _controller.GetLatest(suiteId);

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Approve_Should_ReturnOkWithApprovedProposal()
    {
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var proposal = CreateProposal(suiteId, ProposalStatus.Approved, ProposalSource.Ai);
        proposal.ProposalId = proposalId;
        proposal.ReviewNotes = "Auto-approved after review";

        _approveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ApproveApiTestOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ApproveApiTestOrderCommand, CancellationToken>((command, _) => command.Result = proposal)
            .Returns(Task.CompletedTask);

        var result = await _controller.Approve(suiteId, proposalId, new ApproveApiTestOrderRequest
        {
            RowVersion = "AAAAAQ==",
            ReviewNotes = "Auto-approved after review",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(proposal);
    }

    [Fact]
    public async Task Approve_Should_MapIdsRowVersionReviewNotesAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        ApproveApiTestOrderCommand captured = null!;

        _approveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ApproveApiTestOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ApproveApiTestOrderCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.Result = CreateProposal(suiteId, ProposalStatus.Approved, ProposalSource.Ai);
            })
            .Returns(Task.CompletedTask);

        await _controller.Approve(suiteId, proposalId, new ApproveApiTestOrderRequest
        {
            RowVersion = "row-version-1",
            ReviewNotes = "Looks good",
        });

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.ProposalId.Should().Be(proposalId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.RowVersion.Should().Be("row-version-1");
        captured.ReviewNotes.Should().Be("Looks good");
    }

    [Fact]
    public async Task Approve_Should_PropagateConflictException()
    {
        _approveHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ApproveApiTestOrderCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("TEST_ORDER_CONCURRENCY", "Proposal changed"));

        var act = () => _controller.Approve(Guid.NewGuid(), Guid.NewGuid(), new ApproveApiTestOrderRequest
        {
            RowVersion = "row-version-2",
            ReviewNotes = "Retry later",
        });

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*Proposal changed*");
    }

    [Fact]
    public async Task Reject_Should_ReturnOkWithRejectedProposal()
    {
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var proposal = CreateProposal(suiteId, ProposalStatus.Rejected, ProposalSource.Ai);
        proposal.ProposalId = proposalId;
        proposal.ReviewNotes = "Dependency chain is incomplete";

        _rejectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<RejectApiTestOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<RejectApiTestOrderCommand, CancellationToken>((command, _) => command.Result = proposal)
            .Returns(Task.CompletedTask);

        var result = await _controller.Reject(suiteId, proposalId, new RejectApiTestOrderRequest
        {
            RowVersion = "AAAAAg==",
            ReviewNotes = "Dependency chain is incomplete",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(proposal);
    }

    [Fact]
    public async Task Reject_Should_MapIdsRowVersionReviewNotesAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        RejectApiTestOrderCommand captured = null!;

        _rejectHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<RejectApiTestOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<RejectApiTestOrderCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.Result = CreateProposal(suiteId, ProposalStatus.Rejected, ProposalSource.User);
            })
            .Returns(Task.CompletedTask);

        await _controller.Reject(suiteId, proposalId, new RejectApiTestOrderRequest
        {
            RowVersion = "reject-rv",
            ReviewNotes = "Rejected for missing auth ordering",
        });

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.ProposalId.Should().Be(proposalId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.RowVersion.Should().Be("reject-rv");
        captured.ReviewNotes.Should().Be("Rejected for missing auth ordering");
    }

    [Fact]
    public async Task Reorder_Should_ReturnOkWithReorderedProposal()
    {
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var proposal = CreateProposal(suiteId, ProposalStatus.Pending, ProposalSource.User);
        proposal.ProposalId = proposalId;
        proposal.UserModifiedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = Guid.NewGuid(), HttpMethod = "POST", Path = "/auth/login", OrderIndex = 1 },
            new() { EndpointId = Guid.NewGuid(), HttpMethod = "GET", Path = "/projects", OrderIndex = 2 },
        };

        _reorderHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ReorderApiTestOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ReorderApiTestOrderCommand, CancellationToken>((command, _) => command.Result = proposal)
            .Returns(Task.CompletedTask);

        var result = await _controller.Reorder(suiteId, proposalId, new ReorderApiTestOrderRequest
        {
            RowVersion = "AAAAAw==",
            OrderedEndpointIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
            ReviewNotes = "Move auth first",
        });

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(proposal);
    }

    [Fact]
    public async Task Reorder_Should_MapIdsRowVersionOrderedEndpointsAndReviewNotes()
    {
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var orderedIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ReorderApiTestOrderCommand captured = null!;

        _reorderHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ReorderApiTestOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ReorderApiTestOrderCommand, CancellationToken>((command, _) =>
            {
                captured = command;
                command.Result = CreateProposal(suiteId, ProposalStatus.Pending, ProposalSource.System);
            })
            .Returns(Task.CompletedTask);

        await _controller.Reorder(suiteId, proposalId, new ReorderApiTestOrderRequest
        {
            RowVersion = "reorder-rv",
            OrderedEndpointIds = orderedIds,
            ReviewNotes = "Move dependency producer earlier",
        });

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.ProposalId.Should().Be(proposalId);
        captured.CurrentUserId.Should().Be(_currentUserId);
        captured.RowVersion.Should().Be("reorder-rv");
        captured.OrderedEndpointIds.Should().BeEquivalentTo(orderedIds);
        captured.ReviewNotes.Should().Be("Move dependency producer earlier");
    }

    [Fact]
    public async Task GetGateStatus_Should_ReturnOkWithPayload()
    {
        var suiteId = Guid.NewGuid();
        var payload = new ApiTestOrderGateStatusModel
        {
            TestSuiteId = suiteId,
            IsGatePassed = true,
            ActiveProposalId = Guid.NewGuid(),
            ActiveProposalStatus = ProposalStatus.Approved,
            ReasonCode = "APPROVED_ORDER_EXISTS",
            OrderSize = 6,
            EvaluatedAt = DateTimeOffset.UtcNow,
        };

        _gateStatusHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetApiTestOrderGateStatusQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var result = await _controller.GetGateStatus(suiteId);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(payload);
    }

    [Fact]
    public async Task GetGateStatus_Should_MapSuiteAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        GetApiTestOrderGateStatusQuery captured = null!;

        _gateStatusHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetApiTestOrderGateStatusQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetApiTestOrderGateStatusQuery, CancellationToken>((query, _) => captured = query)
            .ReturnsAsync(new ApiTestOrderGateStatusModel
            {
                TestSuiteId = suiteId,
                IsGatePassed = false,
                ReasonCode = "PENDING_REVIEW",
                ActiveProposalStatus = ProposalStatus.Pending,
                OrderSize = 3,
                EvaluatedAt = DateTimeOffset.UtcNow,
            });

        await _controller.GetGateStatus(suiteId);

        captured.Should().NotBeNull();
        captured.TestSuiteId.Should().Be(suiteId);
        captured.CurrentUserId.Should().Be(_currentUserId);
    }

    private static ApiTestOrderProposalModel CreateProposal(Guid suiteId, ProposalStatus status, ProposalSource source)
    {
        return new ApiTestOrderProposalModel
        {
            ProposalId = Guid.NewGuid(),
            TestSuiteId = suiteId,
            ProposalNumber = 2,
            Status = status,
            Source = source,
            RowVersion = "AAAAAQ==",
            ProposedOrder = new List<ApiOrderItemModel>
            {
                new()
                {
                    EndpointId = Guid.NewGuid(),
                    HttpMethod = "POST",
                    Path = "/auth/login",
                    OrderIndex = 1,
                    DependsOnEndpointIds = new List<Guid>(),
                    ReasonCodes = new List<string> { "AUTH_FIRST" },
                    IsAuthRelated = true,
                },
                new()
                {
                    EndpointId = Guid.NewGuid(),
                    HttpMethod = "GET",
                    Path = "/projects",
                    OrderIndex = 2,
                    DependsOnEndpointIds = new List<Guid>(),
                    ReasonCodes = new List<string> { "READ_AFTER_AUTH" },
                    IsAuthRelated = false,
                },
            },
            ReviewNotes = "Ready for review",
            AiReasoning = "Authenticate before dependent endpoints",
        };
    }
}
