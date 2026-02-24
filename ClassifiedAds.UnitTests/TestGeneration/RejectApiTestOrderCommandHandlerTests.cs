using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class RejectApiTestOrderCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestOrderProposal, Guid>> _proposalRepoMock;
    private readonly Mock<IApiTestOrderService> _orderServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly RejectApiTestOrderCommandHandler _handler;

    public RejectApiTestOrderCommandHandlerTests()
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

        _handler = new RejectApiTestOrderCommandHandler(
            _suiteRepoMock.Object,
            _proposalRepoMock.Object,
            _orderServiceMock.Object,
            new Mock<ILogger<RejectApiTestOrderCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_SetRejectedStatus_AndUpdateSuiteApprovalState()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        var suite = new TestSuite
        {
            Id = suiteId,
            CreatedById = userId,
            Name = "Suite",
            ApprovalStatus = ApprovalStatus.PendingReview,
            ApprovedById = userId,
            ApprovedAt = DateTimeOffset.UtcNow,
        };

        var proposal = new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            ProposalNumber = 1,
            Status = ProposalStatus.Pending,
            ProposedOrder = "[{\"orderIndex\":1}]",
        };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(proposal);
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(It.IsAny<string>()))
            .Returns(new List<ApiOrderItemModel>
            {
                new() { EndpointId = Guid.NewGuid(), HttpMethod = "POST", Path = "/api/users", OrderIndex = 1 },
            });

        var command = new RejectApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            ReviewNotes = "  Need manual verification first  ",
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        proposal.Status.Should().Be(ProposalStatus.Rejected);
        proposal.ReviewedById.Should().Be(userId);
        proposal.ReviewedAt.Should().NotBeNull();
        proposal.ReviewNotes.Should().Be("Need manual verification first");

        suite.ApprovalStatus.Should().Be(ApprovalStatus.Rejected);
        suite.ApprovedById.Should().BeNull();
        suite.ApprovedAt.Should().BeNull();

        command.Result.Should().NotBeNull();
        command.Result.Status.Should().Be(ProposalStatus.Rejected);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenReviewNotesEmpty()
    {
        // Arrange
        var command = new RejectApiTestOrderCommand
        {
            TestSuiteId = Guid.NewGuid(),
            ProposalId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            ReviewNotes = "   ",
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowConflictException_WhenConcurrencyConflictOccurs()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();

        var suite = new TestSuite
        {
            Id = suiteId,
            CreatedById = userId,
            Name = "Suite",
        };

        var proposal = new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            ProposalNumber = 1,
            Status = ProposalStatus.Pending,
            ProposedOrder = "[{\"orderIndex\":1}]",
        };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(proposal);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));
        _proposalRepoMock.Setup(x => x.IsDbUpdateConcurrencyException(It.IsAny<Exception>()))
            .Returns(true);

        var command = new RejectApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            ReviewNotes = "Reject due to inconsistent dependency chain",
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .Where(ex => ex.ReasonCode == TestOrderReasonCodes.ConcurrencyConflict);
    }
}
