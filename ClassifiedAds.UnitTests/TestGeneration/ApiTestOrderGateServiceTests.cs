using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Constants;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// TS-05: Gate fail -> HTTP 409 ORDER_CONFIRMATION_REQUIRED.
/// TS-06: Gate pass -> Returns applied order.
/// </summary>
public class ApiTestOrderGateServiceTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestOrderProposal, Guid>> _proposalRepoMock;
    private readonly Mock<IApiTestOrderService> _orderServiceMock;
    private readonly ApiTestOrderGateService _gateService;

    public ApiTestOrderGateServiceTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _proposalRepoMock = new Mock<IRepository<TestOrderProposal, Guid>>();
        _orderServiceMock = new Mock<IApiTestOrderService>();

        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite>().AsQueryable());
        _proposalRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestOrderProposal>().AsQueryable());

        _gateService = new ApiTestOrderGateService(
            _suiteRepoMock.Object,
            _proposalRepoMock.Object,
            _orderServiceMock.Object);
    }

    [Fact]
    public async Task GetGateStatusAsync_Should_ReturnGateFailed_WhenNoActiveProposal()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var suite = new TestSuite { Id = suiteId, Name = "Test Suite" };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync((TestOrderProposal)null);

        // Act
        var result = await _gateService.GetGateStatusAsync(suiteId);

        // Assert
        result.IsGatePassed.Should().BeFalse();
        result.ReasonCode.Should().Be(TestOrderReasonCodes.OrderConfirmationRequired);
        result.ActiveProposalId.Should().BeNull();
        result.OrderSize.Should().Be(0);
    }

    [Fact]
    public async Task GetGateStatusAsync_Should_ReturnGatePassed_WhenApprovedProposalWithAppliedOrder()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var suite = new TestSuite { Id = suiteId, Name = "Test Suite" };
        var proposal = new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            Status = ProposalStatus.Approved,
            AppliedOrder = "[{\"endpointId\":\"00000000-0000-0000-0000-000000000001\",\"orderIndex\":1}]",
        };
        var appliedItems = new List<ApiOrderItemModel>
        {
            new() { EndpointId = Guid.NewGuid(), HttpMethod = "POST", Path = "/api/auth/login", OrderIndex = 1 },
            new() { EndpointId = Guid.NewGuid(), HttpMethod = "POST", Path = "/api/users", OrderIndex = 2 },
        };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(proposal);
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(proposal.AppliedOrder))
            .Returns(appliedItems);

        // Act
        var result = await _gateService.GetGateStatusAsync(suiteId);

        // Assert
        result.IsGatePassed.Should().BeTrue();
        result.ReasonCode.Should().BeNull();
        result.ActiveProposalId.Should().Be(proposalId);
        result.ActiveProposalStatus.Should().Be(ProposalStatus.Approved);
        result.OrderSize.Should().Be(2);
    }

    [Fact]
    public async Task RequireApprovedOrderAsync_Should_ThrowConflictException_WhenGateFails()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var suite = new TestSuite { Id = suiteId, Name = "Test Suite" };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync((TestOrderProposal)null);

        // Act
        var act = () => _gateService.RequireApprovedOrderAsync(suiteId);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .Where(ex => ex.ReasonCode == TestOrderReasonCodes.OrderConfirmationRequired);
    }

    [Fact]
    public async Task RequireApprovedOrderAsync_Should_ReturnAppliedOrder_WhenGatePasses()
    {
        // Arrange
        var suiteId = Guid.NewGuid();
        var proposalId = Guid.NewGuid();
        var suite = new TestSuite { Id = suiteId, Name = "Test Suite" };
        var appliedOrderJson = "[{\"endpointId\":\"00000000-0000-0000-0000-000000000001\",\"orderIndex\":1}]";
        var proposal = new TestOrderProposal
        {
            Id = proposalId,
            TestSuiteId = suiteId,
            Status = ProposalStatus.Approved,
            AppliedOrder = appliedOrderJson,
        };
        var appliedItems = new List<ApiOrderItemModel>
        {
            new() { EndpointId = Guid.NewGuid(), HttpMethod = "POST", Path = "/api/auth/login", OrderIndex = 1 },
        };

        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _proposalRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestOrderProposal>>()))
            .ReturnsAsync(proposal);
        _orderServiceMock.Setup(x => x.DeserializeOrderJson(appliedOrderJson))
            .Returns(appliedItems);

        // Act
        var result = await _gateService.RequireApprovedOrderAsync(suiteId);

        // Assert
        result.Should().HaveCount(1);
        result[0].Path.Should().Be("/api/auth/login");
    }

    [Fact]
    public async Task GetGateStatusAsync_Should_ThrowNotFoundException_WhenSuiteNotFound()
    {
        // Arrange
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync((TestSuite)null);

        // Act
        var act = () => _gateService.GetGateStatusAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
