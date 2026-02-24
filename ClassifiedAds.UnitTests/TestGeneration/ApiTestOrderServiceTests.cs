using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class ApiTestOrderServiceTests
{
    private readonly Mock<IApiEndpointMetadataService> _endpointMetadataServiceMock;
    private readonly ApiTestOrderService _service;

    public ApiTestOrderServiceTests()
    {
        _endpointMetadataServiceMock = new Mock<IApiEndpointMetadataService>();
        _service = new ApiTestOrderService(
            _endpointMetadataServiceMock.Object,
            new ApiTestOrderAlgorithm());
    }

    [Fact]
    public async Task BuildProposalOrderAsync_Should_OrderAuthFirst_AndDependencyAware()
    {
        // Arrange
        var authEndpointId = Guid.NewGuid();
        var createUserEndpointId = Guid.NewGuid();
        var getUserEndpointId = Guid.NewGuid();

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = getUserEndpointId,
                    HttpMethod = "GET",
                    Path = "/api/users/{id}",
                    IsAuthRelated = false,
                    DependsOnEndpointIds = new [] { createUserEndpointId },
                },
                new()
                {
                    EndpointId = authEndpointId,
                    HttpMethod = "POST",
                    Path = "/api/auth/login",
                    IsAuthRelated = true,
                },
                new()
                {
                    EndpointId = createUserEndpointId,
                    HttpMethod = "POST",
                    Path = "/api/users",
                    IsAuthRelated = false,
                },
            });

        // Act
        var result = await _service.BuildProposalOrderAsync(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<Guid>());

        // Assert
        result.Should().HaveCount(3);
        result[0].EndpointId.Should().Be(authEndpointId);
        result[0].OrderIndex.Should().Be(1);
        result[0].ReasonCodes.Should().Contain("AUTH_FIRST");

        result[1].EndpointId.Should().Be(createUserEndpointId);
        result[1].OrderIndex.Should().Be(2);

        result[2].EndpointId.Should().Be(getUserEndpointId);
        result[2].OrderIndex.Should().Be(3);
    }

    [Fact]
    public async Task BuildProposalOrderAsync_Should_RespectDependencyGraph_WhenMetadataIsUnsorted()
    {
        // Arrange
        var authEndpointId = Guid.NewGuid();
        var createUserEndpointId = Guid.NewGuid();
        var updateUserEndpointId = Guid.NewGuid();
        var deleteUserEndpointId = Guid.NewGuid();
        var listUserEndpointId = Guid.NewGuid();

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = deleteUserEndpointId,
                    HttpMethod = "DELETE",
                    Path = "/api/users/{id}",
                    IsAuthRelated = false,
                    DependsOnEndpointIds = new[] { createUserEndpointId },
                },
                new()
                {
                    EndpointId = listUserEndpointId,
                    HttpMethod = "GET",
                    Path = "/api/users",
                    IsAuthRelated = false,
                },
                new()
                {
                    EndpointId = updateUserEndpointId,
                    HttpMethod = "PATCH",
                    Path = "/api/users/{id}",
                    IsAuthRelated = false,
                    DependsOnEndpointIds = new[] { createUserEndpointId },
                },
                new()
                {
                    EndpointId = authEndpointId,
                    HttpMethod = "POST",
                    Path = "/api/auth/login",
                    IsAuthRelated = true,
                },
                new()
                {
                    EndpointId = createUserEndpointId,
                    HttpMethod = "POST",
                    Path = "/api/users",
                    IsAuthRelated = false,
                },
            });

        // Act
        var result = await _service.BuildProposalOrderAsync(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<Guid>());

        // Assert
        result.Should().HaveCount(5);
        result[0].EndpointId.Should().Be(authEndpointId);
        result[0].ReasonCodes.Should().Contain("AUTH_FIRST");

        var orderByEndpointId = result.ToDictionary(x => x.EndpointId, x => x.OrderIndex);
        orderByEndpointId[createUserEndpointId].Should().BeLessThan(orderByEndpointId[updateUserEndpointId]);
        orderByEndpointId[createUserEndpointId].Should().BeLessThan(orderByEndpointId[deleteUserEndpointId]);
        result.Should().BeInAscendingOrder(x => x.OrderIndex);
    }

    [Fact]
    public async Task BuildProposalOrderAsync_Should_BreakCycleDeterministically_WhenDependenciesAreCircular()
    {
        // Arrange
        var authEndpointId = Guid.NewGuid();
        var endpointA = Guid.NewGuid();
        var endpointB = Guid.NewGuid();

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = endpointA,
                    HttpMethod = "GET",
                    Path = "/api/projects/{id}",
                    IsAuthRelated = false,
                    DependsOnEndpointIds = new[] { endpointB },
                },
                new()
                {
                    EndpointId = endpointB,
                    HttpMethod = "POST",
                    Path = "/api/projects",
                    IsAuthRelated = false,
                    DependsOnEndpointIds = new[] { endpointA },
                },
                new()
                {
                    EndpointId = authEndpointId,
                    HttpMethod = "POST",
                    Path = "/api/auth/login",
                    IsAuthRelated = true,
                },
            });

        // Act
        var result = await _service.BuildProposalOrderAsync(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<Guid>());

        // Assert
        result.Should().HaveCount(3);
        result[0].EndpointId.Should().Be(authEndpointId);
        result[1].EndpointId.Should().Be(endpointB);
        result[1].ReasonCodes.Should().Contain("CYCLE_BREAK_FALLBACK");
        result[2].EndpointId.Should().Be(endpointA);
    }

    [Fact]
    public async Task BuildProposalOrderAsync_Should_ApplyDeterministicTieBreak_WhenNoDependenciesExist()
    {
        // Arrange
        var endpointGetB = Guid.NewGuid();
        var endpointGetA = Guid.NewGuid();
        var endpointPost = Guid.NewGuid();

        _endpointMetadataServiceMock.Setup(x => x.GetEndpointMetadataAsync(
                It.IsAny<Guid>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiEndpointMetadataDto>
            {
                new()
                {
                    EndpointId = endpointGetB,
                    HttpMethod = "GET",
                    Path = "/api/b",
                    IsAuthRelated = false,
                },
                new()
                {
                    EndpointId = endpointGetA,
                    HttpMethod = "GET",
                    Path = "/api/a",
                    IsAuthRelated = false,
                },
                new()
                {
                    EndpointId = endpointPost,
                    HttpMethod = "POST",
                    Path = "/api/z",
                    IsAuthRelated = false,
                },
            });

        // Act
        var result = await _service.BuildProposalOrderAsync(Guid.NewGuid(), Guid.NewGuid(), Array.Empty<Guid>());

        // Assert
        result.Should().HaveCount(3);
        result[0].EndpointId.Should().Be(endpointPost);
        result[1].EndpointId.Should().Be(endpointGetA);
        result[2].EndpointId.Should().Be(endpointGetB);
        result.Should().OnlyContain(x => x.ReasonCodes.Contains("DETERMINISTIC_TIE_BREAK"));
    }

    [Fact]
    public void ValidateReorderedEndpointSet_Should_ThrowValidationException_WhenIdsDuplicate()
    {
        // Arrange
        var endpoint1 = Guid.NewGuid();
        var endpoint2 = Guid.NewGuid();
        var proposal = new[]
        {
            new ClassifiedAds.Modules.TestGeneration.Models.ApiOrderItemModel
            {
                EndpointId = endpoint1,
                HttpMethod = "POST",
                Path = "/api/users",
                OrderIndex = 1,
            },
            new ClassifiedAds.Modules.TestGeneration.Models.ApiOrderItemModel
            {
                EndpointId = endpoint2,
                HttpMethod = "GET",
                Path = "/api/users/{id}",
                OrderIndex = 2,
            },
        };

        // Act
        var action = () => _service.ValidateReorderedEndpointSet(
            proposal,
            new[] { endpoint1, endpoint1 });

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*INVALID_ORDER_SET*");
    }
}

public class ApiTestOrderServiceValidationTests
{
    [Fact]
    public void ValidateReorderedEndpointSet_Should_ThrowValidationException_WhenOutOfScopeEndpointExists()
    {
        // Arrange
        var endpointMetadataServiceMock = new Mock<IApiEndpointMetadataService>();
        var service = new ApiTestOrderService(
            endpointMetadataServiceMock.Object,
            new ApiTestOrderAlgorithm());
        var endpoint1 = Guid.NewGuid();
        var endpoint2 = Guid.NewGuid();
        var outOfScopeEndpoint = Guid.NewGuid();

        var proposedOrder = new[]
        {
            new ClassifiedAds.Modules.TestGeneration.Models.ApiOrderItemModel
            {
                EndpointId = endpoint1,
                HttpMethod = "POST",
                Path = "/api/users",
                OrderIndex = 1,
            },
            new ClassifiedAds.Modules.TestGeneration.Models.ApiOrderItemModel
            {
                EndpointId = endpoint2,
                HttpMethod = "GET",
                Path = "/api/users/{id}",
                OrderIndex = 2,
            },
        };

        // Act
        var action = () => service.ValidateReorderedEndpointSet(
            proposedOrder,
            new[] { endpoint1, outOfScopeEndpoint });

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*INVALID_ORDER_SET*");
    }
}
