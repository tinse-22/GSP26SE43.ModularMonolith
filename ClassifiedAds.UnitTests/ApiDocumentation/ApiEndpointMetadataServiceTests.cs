using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApiHttpMethod = ClassifiedAds.Modules.ApiDocumentation.Entities.HttpMethod;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class ApiEndpointMetadataServiceTests
{
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock;
    private readonly Mock<IRepository<ApiEndpoint, Guid>> _endpointRepoMock;
    private readonly Mock<IRepository<EndpointParameter, Guid>> _parameterRepoMock;
    private readonly Mock<IRepository<EndpointResponse, Guid>> _responseRepoMock;
    private readonly Mock<IRepository<EndpointSecurityReq, Guid>> _securityRepoMock;

    private readonly List<ApiSpecification> _specifications = new();
    private readonly List<ApiEndpoint> _endpoints = new();
    private readonly List<EndpointParameter> _parameters = new();
    private readonly List<EndpointResponse> _responses = new();
    private readonly List<EndpointSecurityReq> _securityRequirements = new();

    private readonly ApiEndpointMetadataService _service;

    public ApiEndpointMetadataServiceTests()
    {
        _specRepoMock = new Mock<IRepository<ApiSpecification, Guid>>();
        _endpointRepoMock = new Mock<IRepository<ApiEndpoint, Guid>>();
        _parameterRepoMock = new Mock<IRepository<EndpointParameter, Guid>>();
        _responseRepoMock = new Mock<IRepository<EndpointResponse, Guid>>();
        _securityRepoMock = new Mock<IRepository<EndpointSecurityReq, Guid>>();

        _specRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _specifications.AsQueryable());
        _endpointRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _endpoints.AsQueryable());
        _parameterRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _parameters.AsQueryable());
        _responseRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _responses.AsQueryable());
        _securityRepoMock.Setup(x => x.GetQueryableSet()).Returns(() => _securityRequirements.AsQueryable());

        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync((IQueryable<ApiSpecification> query) => query.FirstOrDefault());

        _endpointRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<ApiEndpoint>>()))
            .ReturnsAsync((IQueryable<ApiEndpoint> query) => query.ToList());
        _parameterRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<EndpointParameter>>()))
            .ReturnsAsync((IQueryable<EndpointParameter> query) => query.ToList());
        _responseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<EndpointResponse>>()))
            .ReturnsAsync((IQueryable<EndpointResponse> query) => query.ToList());
        _securityRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<EndpointSecurityReq>>()))
            .ReturnsAsync((IQueryable<EndpointSecurityReq> query) => query.ToList());

        _service = new ApiEndpointMetadataService(
            _specRepoMock.Object,
            _endpointRepoMock.Object,
            _parameterRepoMock.Object,
            _responseRepoMock.Object,
            _securityRepoMock.Object);
    }

    [Fact]
    public async Task GetEndpointMetadataAsync_Should_AddResourceAndAuthDependencies_ForSecuredConsumer()
    {
        // Arrange
        var specId = Guid.NewGuid();
        var authEndpointId = Guid.NewGuid();
        var createUserEndpointId = Guid.NewGuid();
        var getUserEndpointId = Guid.NewGuid();

        _specifications.Add(new ApiSpecification { Id = specId, Name = "Spec" });

        _endpoints.AddRange(new[]
        {
            new ApiEndpoint
            {
                Id = authEndpointId,
                ApiSpecId = specId,
                HttpMethod = ApiHttpMethod.POST,
                Path = "/api/auth/login",
                OperationId = "login",
            },
            new ApiEndpoint
            {
                Id = createUserEndpointId,
                ApiSpecId = specId,
                HttpMethod = ApiHttpMethod.POST,
                Path = "/api/users",
                OperationId = "createUser",
            },
            new ApiEndpoint
            {
                Id = getUserEndpointId,
                ApiSpecId = specId,
                HttpMethod = ApiHttpMethod.GET,
                Path = "/api/users/{id}",
                OperationId = "getUser",
            },
        });

        _parameters.Add(new EndpointParameter
        {
            Id = Guid.NewGuid(),
            EndpointId = getUserEndpointId,
            Name = "userId",
            Location = ParameterLocation.Path,
            IsRequired = true,
            Schema = "{\"$ref\":\"#/components/schemas/User\"}",
        });

        _responses.Add(new EndpointResponse
        {
            Id = Guid.NewGuid(),
            EndpointId = createUserEndpointId,
            StatusCode = 201,
            Schema = "{\"$ref\":\"#/components/schemas/User\"}",
        });

        _securityRequirements.Add(new EndpointSecurityReq
        {
            Id = Guid.NewGuid(),
            EndpointId = getUserEndpointId,
            SecurityType = SecurityType.Bearer,
            SchemeName = "Bearer",
        });

        // Act
        var result = await _service.GetEndpointMetadataAsync(specId, new[] { authEndpointId, createUserEndpointId, getUserEndpointId });

        // Assert
        result.Should().HaveCount(3);

        var authMetadata = result.Single(x => x.EndpointId == authEndpointId);
        authMetadata.IsAuthRelated.Should().BeTrue();

        var getUserMetadata = result.Single(x => x.EndpointId == getUserEndpointId);
        getUserMetadata.DependsOnEndpointIds.Should().Contain(createUserEndpointId);
        getUserMetadata.DependsOnEndpointIds.Should().Contain(authEndpointId);
        getUserMetadata.DependsOnEndpointIds.Should().NotContain(getUserEndpointId);
        getUserMetadata.DependsOnEndpointIds.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetEndpointMetadataAsync_Should_InferDependencyFromSchemaRefs_ForPostConsumer()
    {
        // Arrange
        var specId = Guid.NewGuid();
        var createAccountEndpointId = Guid.NewGuid();
        var createOrderEndpointId = Guid.NewGuid();

        _specifications.Add(new ApiSpecification { Id = specId, Name = "Spec" });

        _endpoints.AddRange(new[]
        {
            new ApiEndpoint
            {
                Id = createAccountEndpointId,
                ApiSpecId = specId,
                HttpMethod = ApiHttpMethod.POST,
                Path = "/api/accounts",
                OperationId = "createAccount",
            },
            new ApiEndpoint
            {
                Id = createOrderEndpointId,
                ApiSpecId = specId,
                HttpMethod = ApiHttpMethod.POST,
                Path = "/api/orders",
                OperationId = "createOrder",
            },
        });

        _responses.Add(new EndpointResponse
        {
            Id = Guid.NewGuid(),
            EndpointId = createAccountEndpointId,
            StatusCode = 201,
            Schema = "{\"$ref\":\"#/components/schemas/Account\"}",
        });

        _parameters.Add(new EndpointParameter
        {
            Id = Guid.NewGuid(),
            EndpointId = createOrderEndpointId,
            Name = "body",
            Location = ParameterLocation.Body,
            IsRequired = true,
            Schema = "{\"type\":\"object\",\"properties\":{\"account\":{\"$ref\":\"#/components/schemas/Account\"}}}",
        });

        // Act
        var result = await _service.GetEndpointMetadataAsync(specId, new[] { createAccountEndpointId, createOrderEndpointId });

        // Assert
        var createOrderMetadata = result.Single(x => x.EndpointId == createOrderEndpointId);
        createOrderMetadata.DependsOnEndpointIds.Should().Contain(createAccountEndpointId);
    }

    [Fact]
    public async Task GetEndpointMetadataAsync_Should_ThrowValidationException_WhenSelectedEndpointMissing()
    {
        // Arrange
        var specId = Guid.NewGuid();
        var existingEndpointId = Guid.NewGuid();
        var missingEndpointId = Guid.NewGuid();

        _specifications.Add(new ApiSpecification { Id = specId, Name = "Spec" });
        _endpoints.Add(new ApiEndpoint
        {
            Id = existingEndpointId,
            ApiSpecId = specId,
            HttpMethod = ApiHttpMethod.GET,
            Path = "/api/health",
            OperationId = "health",
        });

        // Act
        var act = () => _service.GetEndpointMetadataAsync(specId, new[] { existingEndpointId, missingEndpointId });

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }
}
