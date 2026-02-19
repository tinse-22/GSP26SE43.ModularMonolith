using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// TS-03: Approve without reorder -> Status=Approved, AppliedOrder=ProposedOrder.
/// TS-04: Approve with reorder -> Status=ModifiedAndApproved, AppliedOrder=UserModifiedOrder.
/// </summary>
public class ApproveApiTestOrderCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestOrderProposal, Guid>> _proposalRepoMock;
    private readonly Mock<IApiTestOrderService> _orderServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ApproveApiTestOrderCommandHandler _handler;

    public ApproveApiTestOrderCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _proposalRepoMock = new Mock<IRepository<TestOrderProposal, Guid>>();
        _orderServiceMock = new Mock<IApiTestOrderService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _proposalRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _handler = new ApproveApiTestOrderCommandHandler(
            _suiteRepoMock.Object,
            _proposalRepoMock.Object,
            _orderServiceMock.Object,
            new Mock<ILogger<ApproveApiTestOrderCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_SetStatusApproved_WhenNoUserModifiedOrder()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, userId);
        var proposal = CreatePendingProposal(proposalId, suiteId, hasUserReorder: false);

        SetupEntitiesFound(suite, proposal);
        SetupOrderDeserialization(hasUserModifiedOrder: false);
        SetupSerializeOrder();

        var command = new ApproveApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3

 }),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        proposal.Status.Should().Be(ProposalStatus.Approved);
        proposal.ReviewedById.Should().Be(userId);
        proposal.ReviewedAt.Should().NotBeNull();
        proposal.AppliedOrder.Should().NotBeNullOrWhiteSpace();
        proposal.AppliedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_SetStatusModifiedAndApproved_WhenUserModifiedOrderExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, userId);
        var proposal = CreatePendingProposal(proposalId, suiteId, hasUserReorder: true);

        SetupEntitiesFound(suite, proposal);
        SetupOrderDeserialization(hasUserModifiedOrder: true);
        SetupSerializeOrder();

        var command = new ApproveApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        proposal.Status.Should().Be(ProposalStatus.ModifiedAndApproved);
    }

    [Fact]
    public async Task HandleAsync_Should_UpdateSuiteApprovalFields_WhenApproved()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, userId);
        var proposal = CreatePendingProposal(proposalId, suiteId, hasUserReorder: false);

        SetupEntitiesFound(suite, proposal);
        SetupOrderDeserialization(hasUserModifiedOrder: false);
        SetupSerializeOrder();

        var command = new ApproveApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        suite.ApprovalStatus.Should().Be(ApprovalStatus.Approved);
        suite.ApprovedById.Should().Be(userId);
        suite.ApprovedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_SetAppliedOrderFromProposedOrder_WhenNoReorder()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, userId);
        var proposal = CreatePendingProposal(proposalId, suiteId, hasUserReorder: false);

        SetupEntitiesFound(suite, proposal);
        SetupOrderDeserialization(hasUserModifiedOrder: false);

        var expectedJson = "[{\"orderIndex\":1}]";
        _orderServiceMock.Setup(x => x.SerializeOrderJson(It.IsAny<IReadOnlyCollection<ApiOrderItemModel>>()))
            .Returns(expectedJson);

        var command = new ApproveApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        proposal.AppliedOrder.Should().Be(expectedJson);
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnEarly_WhenProposalAlreadyApproved()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, userId);
        var proposal = new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            Status = ProposalStatus.Approved,
            AppliedOrder = "[{\"orderIndex\":1}]",
        };

        SetupEntitiesFound(suite, proposal);
        SetupOrderDeserialization(hasUserModifiedOrder: false);

        var command = new ApproveApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert - should not call transaction
        _unitOfWorkMock.Verify(x => x.ExecuteInTransactionAsync(
            It.IsAny<Func<CancellationToken, Task>>(),
            It.IsAny<IsolationLevel>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenProposalNotPending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, userId);
        var proposal = new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            Status = ProposalStatus.Rejected,
        };

        SetupEntitiesFound(suite, proposal);

        var command = new ApproveApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    #region Helpers

    private static TestSuite CreateSuite(Guid suiteId, Guid ownerId)
    {
        return new TestSuite
        {
            Id = suiteId,
            CreatedById = ownerId,
            Name = "Test Suite",
            ApprovalStatus = ApprovalStatus.PendingReview,
        };
    }

    private static TestOrderProposal CreatePendingProposal(Guid proposalId, Guid suiteId, bool hasUserReorder)
    {
        return new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            ProposalNumber = 1,
            Status = ProposalStatus.Pending,
            ProposedOrder = "[{\"endpointId\":\"00000000-0000-0000-0000-000000000001\",\"orderIndex\":1}]",
            UserModifiedOrder = hasUserReorder
                ? "[{\"endpointId\":\"00000000-0000-0000-0000-000000000001\",\"orderIndex\":1}]"
                : null,
        };
    }

    private void SetupEntitiesFound(TestSuite suite, TestOrderProposal proposal)
    {
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(proposal);
    }

    private void SetupOrderDeserialization(bool hasUserModifiedOrder)
    {
        var proposedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = Guid.NewGuid(), HttpMethod = "POST", Path = "/api/auth/login", OrderIndex = 1 },
        };

        _orderServiceMock.Setup(x => x.DeserializeOrderJson(It.Is<string>(s => !string.IsNullOrWhiteSpace(s))))
            .Returns(proposedOrder);
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(It.Is<string>(s => string.IsNullOrWhiteSpace(s))))
            .Returns(new List<ApiOrderItemModel>());
    }

    private void SetupSerializeOrder()
    {
        _orderServiceMock.Setup(x => x.SerializeOrderJson(It.IsAny<IReadOnlyCollection<ApiOrderItemModel>>()))
            .Returns("[{\"orderIndex\":1}]");
    }

    #endregion
}
