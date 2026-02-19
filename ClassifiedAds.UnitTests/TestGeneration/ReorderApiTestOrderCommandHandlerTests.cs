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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// TS-02: Reorder with stale rowVersion -> 409 conflict concurrency.
/// </summary>
public class ReorderApiTestOrderCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestOrderProposal, Guid>> _proposalRepoMock;
    private readonly Mock<IApiTestOrderService> _orderServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ReorderApiTestOrderCommandHandler _handler;

    public ReorderApiTestOrderCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _proposalRepoMock = new Mock<IRepository<TestOrderProposal, Guid>>();
        _orderServiceMock = new Mock<IApiTestOrderService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _proposalRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite>().AsQueryable());
        _proposalRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestOrderProposal>().AsQueryable());

        _handler = new ReorderApiTestOrderCommandHandler(
            _suiteRepoMock.Object,
            _proposalRepoMock.Object,
            _orderServiceMock.Object,
            new Mock<ILogger<ReorderApiTestOrderCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowConflictException_WhenRowVersionIsStale()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var suite = new TestSuite { Id = suiteId, CreatedById = userId, Name = "Suite" };
        var proposal = new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            ProposalNumber = 1,
            Status = ProposalStatus.Pending,
            ProposedOrder = "[{\"endpointId\":\"" + endpointId + "\",\"orderIndex\":1}]",
        };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(proposal);
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(It.IsAny<string>()))
            .Returns(new List<ApiOrderItemModel>
            {
                new() { EndpointId = endpointId, HttpMethod = "POST", Path = "/api/users", OrderIndex = 1 },
            });
        _orderServiceMock.Setup(x => x.ValidateReorderedEndpointSet(
                It.IsAny<IReadOnlyList<ApiOrderItemModel>>(),
                It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(new List<Guid> { endpointId });
        _orderServiceMock.Setup(x => x.SerializeOrderJson(It.IsAny<IReadOnlyCollection<ApiOrderItemModel>>()))
            .Returns("[{\"orderIndex\":1}]");

        // Simulate stale rowVersion: SaveChangesAsync throws concurrency exception
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Concurrency conflict"));
        _proposalRepoMock.Setup(x => x.IsDbUpdateConcurrencyException(It.IsAny<Exception>()))
            .Returns(true);

        var command = new ReorderApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            OrderedEndpointIds = new[] { endpointId },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .Where(ex => ex.ReasonCode == TestOrderReasonCodes.ConcurrencyConflict);
    }

    [Fact]
    public async Task HandleAsync_Should_PersistUserModifiedOrder_WhenValidReorder()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var endpointId1 = Guid.NewGuid();
        var endpointId2 = Guid.NewGuid();

        var suite = new TestSuite { Id = suiteId, CreatedById = userId, Name = "Suite" };
        var proposal = new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            ProposalNumber = 1,
            Status = ProposalStatus.Pending,
            ProposedOrder = "[{\"orderIndex\":1},{\"orderIndex\":2}]",
        };
        var proposedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = endpointId1, HttpMethod = "POST", Path = "/api/users", OrderIndex = 1 },
            new() { EndpointId = endpointId2, HttpMethod = "GET", Path = "/api/users/{id}", OrderIndex = 2 },
        };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(proposal);
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(It.IsAny<string>()))
            .Returns(proposedOrder);
        _orderServiceMock.Setup(x => x.ValidateReorderedEndpointSet(
                It.IsAny<IReadOnlyList<ApiOrderItemModel>>(),
                It.IsAny<IReadOnlyCollection<Guid>>()))
            .Returns(new List<Guid> { endpointId2, endpointId1 });

        var expectedJson = "[{\"endpointId\":\"...\",\"orderIndex\":1}]";
        _orderServiceMock.Setup(x => x.SerializeOrderJson(It.IsAny<IReadOnlyCollection<ApiOrderItemModel>>()))
            .Returns(expectedJson);

        var command = new ReorderApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            OrderedEndpointIds = new[] { endpointId2, endpointId1 },
            ReviewNotes = "Reversed order for testing",
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        proposal.UserModifiedOrder.Should().Be(expectedJson);
        proposal.ReviewNotes.Should().Be("Reversed order for testing");
        proposal.Status.Should().Be(ProposalStatus.Pending);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenProposalNotPending()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var suite = new TestSuite { Id = suiteId, CreatedById = userId, Name = "Suite" };
        var proposal = new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            Status = ProposalStatus.Approved,
        };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(proposal);

        var command = new ReorderApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            ProposalId = proposalId,
            CurrentUserId = userId,
            RowVersion = Convert.ToBase64String(new byte[] { 1, 2, 3 }),
            OrderedEndpointIds = new[] { Guid.NewGuid() },
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
