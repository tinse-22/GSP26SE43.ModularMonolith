using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// TS-01: Propose new order - Tao TestOrderProposal Pending voi ProposalNumber tang 1.
/// </summary>
public class ProposeApiTestOrderCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestOrderProposal, Guid>> _proposalRepoMock;
    private readonly Mock<IApiTestOrderService> _orderServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ProposeApiTestOrderCommandHandler _handler;

    public ProposeApiTestOrderCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _proposalRepoMock = new Mock<IRepository<TestOrderProposal, Guid>>();
        _orderServiceMock = new Mock<IApiTestOrderService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _proposalRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite>().AsQueryable());
        _proposalRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestOrderProposal>().AsQueryable());

        _handler = new ProposeApiTestOrderCommandHandler(
            _suiteRepoMock.Object,
            _proposalRepoMock.Object,
            _orderServiceMock.Object,
            new Mock<ILogger<ProposeApiTestOrderCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_CreatePendingProposal_WithProposalNumber1_WhenNoExistingProposals()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, specId, userId);

        SetupSuiteFound(suite);
        SetupNoExistingProposals();
        SetupBuildOrder(specId);
        SetupSerializeOrder();

        var command = new ProposeApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = userId,
            SpecificationId = specId,
            Source = ProposalSource.Ai,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _proposalRepoMock.Verify(x => x.AddAsync(
            It.Is<TestOrderProposal>(p =>
                p.TestSuiteId == suiteId &&
                p.ProposalNumber == 1 &&
                p.Status == ProposalStatus.Pending &&
                p.Source == ProposalSource.Ai),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_IncrementProposalNumber_WhenExistingProposalsExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, specId, userId);

        SetupSuiteFound(suite);
        SetupExistingProposals(suiteId, maxProposalNumber: 3);
        SetupBuildOrder(specId);
        SetupSerializeOrder();

        var command = new ProposeApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = userId,
            SpecificationId = specId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        _proposalRepoMock.Verify(x => x.AddAsync(
            It.Is<TestOrderProposal>(p => p.ProposalNumber == 4),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_SupersedeExistingPendingProposals()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, specId, userId);
        var existingPending = new TestOrderProposal
        {
            Id = Guid.NewGuid(),
            TestSuiteId = suiteId,
            ProposalNumber = 1,
            Status = ProposalStatus.Pending,
        };

        SetupSuiteFound(suite);
        _proposalRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(new List<TestOrderProposal> { existingPending });
        SetupBuildOrder(specId);
        SetupSerializeOrder();

        var command = new ProposeApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = userId,
            SpecificationId = specId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        existingPending.Status.Should().Be(ProposalStatus.Superseded);
    }

    [Fact]
    public async Task HandleAsync_Should_SetSuiteApprovalStatusToPendingReview()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var suiteId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, specId, userId);

        SetupSuiteFound(suite);
        SetupNoExistingProposals();
        SetupBuildOrder(specId);
        SetupSerializeOrder();

        var command = new ProposeApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = userId,
            SpecificationId = specId,
        };

        // Act
        await _handler.HandleAsync(command);

        // Assert
        suite.ApprovalStatus.Should().Be(ApprovalStatus.PendingReview);
        suite.ApprovedById.Should().BeNull();
        suite.ApprovedAt.Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFoundException_WhenSuiteNotFound()
    {
        // Arrange
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync((TestSuite)null);

        var command = new ProposeApiTestOrderCommand
        {
            TestSuiteId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            SpecificationId = Guid.NewGuid(),
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidationException_WhenUserNotOwner()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var suite = CreateSuite(suiteId, specId, ownerId: Guid.NewGuid());

        SetupSuiteFound(suite);

        var command = new ProposeApiTestOrderCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = Guid.NewGuid(),
            SpecificationId = specId,
        };

        // Act
        var act = () => _handler.HandleAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    #region Helpers

    private static TestSuite CreateSuite(Guid suiteId, Guid specId, Guid ownerId)
    {
        return new TestSuite
        {
            Id = suiteId,
            ApiSpecId = specId,
            CreatedById = ownerId,
            Name = "Test Suite",
            ApprovalStatus = ApprovalStatus.NotApplicable,
        };
    }

    private void SetupSuiteFound(TestSuite suite)
    {
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
    }

    private void SetupNoExistingProposals()
    {
        _proposalRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(new List<TestOrderProposal>());
    }

    private void SetupExistingProposals(Guid suiteId, int maxProposalNumber)
    {
        var proposals = Enumerable.Range(1, maxProposalNumber)
            .Select(n => new TestOrderProposal
            {
                Id = Guid.NewGuid(),
                TestSuiteId = suiteId,
                ProposalNumber = n,
                Status = ProposalStatus.Superseded,
            })
            .ToList();

        _proposalRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(proposals);
    }

    private void SetupBuildOrder(Guid specId)
    {
        _orderServiceMock.Setup(x => x.BuildProposalOrderAsync(
                It.IsAny<Guid>(),
                specId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiOrderItemModel>
            {
                new() { EndpointId = Guid.NewGuid(), HttpMethod = "POST", Path = "/api/auth/login", OrderIndex = 1 },
            });
    }

    private void SetupSerializeOrder()
    {
        _orderServiceMock.Setup(x => x.SerializeOrderJson(It.IsAny<IReadOnlyCollection<ApiOrderItemModel>>()))
            .Returns("[{\"endpointId\":\"00000000-0000-0000-0000-000000000001\"}]");
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(It.IsAny<string>()))
            .Returns(new List<ApiOrderItemModel>
            {
                new() { EndpointId = Guid.NewGuid(), HttpMethod = "POST", Path = "/api/auth/login", OrderIndex = 1 },
            });
    }

    #endregion
}
