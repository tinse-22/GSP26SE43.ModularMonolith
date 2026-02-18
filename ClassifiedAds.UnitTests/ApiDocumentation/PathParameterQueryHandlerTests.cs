using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.ApiDocumentation.Entities;
using ClassifiedAds.Modules.ApiDocumentation.Queries;
using ClassifiedAds.Modules.ApiDocumentation.Services;
using ClassifiedAds.UnitTests.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class GetResolvedUrlQueryHandlerTests
{
    private readonly Mock<IRepository<Project, Guid>> _projectRepoMock;
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock;
    private readonly Mock<IRepository<ApiEndpoint, Guid>> _endpointRepoMock;
    private readonly Mock<IRepository<EndpointParameter, Guid>> _parameterRepoMock;
    private readonly GetResolvedUrlQueryHandler _handler;

    public GetResolvedUrlQueryHandlerTests()
    {
        _projectRepoMock = new Mock<IRepository<Project, Guid>>();
        _specRepoMock = new Mock<IRepository<ApiSpecification, Guid>>();
        _endpointRepoMock = new Mock<IRepository<ApiEndpoint, Guid>>();
        _parameterRepoMock = new Mock<IRepository<EndpointParameter, Guid>>();

        _handler = new GetResolvedUrlQueryHandler(
            _projectRepoMock.Object,
            _specRepoMock.Object,
            _endpointRepoMock.Object,
            _parameterRepoMock.Object,
            new PathParameterTemplateService());
    }

    [Fact]
    public async Task HandleAsync_Should_ApplyExplicitThenFallbackValues_AndResolveUrl()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        SetupRepositories(
            projects: new List<Project>
            {
                new() { Id = projectId, OwnerId = ownerId, Name = "P", Status = ProjectStatus.Active },
            },
            specs: new List<ApiSpecification>
            {
                new() { Id = specId, ProjectId = projectId, Name = "Spec", ParseStatus = ParseStatus.Success, SourceType = SourceType.Manual },
            },
            endpoints: new List<ApiEndpoint>
            {
                new() { Id = endpointId, ApiSpecId = specId, HttpMethod = ClassifiedAds.Modules.ApiDocumentation.Entities.HttpMethod.GET, Path = "/api/users/{userId}/orders/{orderId}/posts/{postId}" },
            },
            parameters: new List<EndpointParameter>
            {
                new() { Id = Guid.NewGuid(), EndpointId = endpointId, Name = "userId", Location = ParameterLocation.Path, DefaultValue = "42", DataType = "integer", IsRequired = true },
                new() { Id = Guid.NewGuid(), EndpointId = endpointId, Name = "orderId", Location = ParameterLocation.Path, Examples = "[\"ord-777\"]", DataType = "string", IsRequired = true },
                new() { Id = Guid.NewGuid(), EndpointId = endpointId, Name = "postId", Location = ParameterLocation.Path, DataType = "string", IsRequired = true },
            });

        var query = new GetResolvedUrlQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            OwnerId = ownerId,
            ParameterValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["userId"] = string.Empty,
                ["POSTID"] = "abc 123/xyz",
            },
        };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.IsFullyResolved.Should().BeTrue();
        result.ResolvedUrl.Should().Be("/api/users/42/orders/ord-777/posts/abc%20123%2Fxyz");
        result.UnresolvedParameters.Should().BeEmpty();
        result.ResolvedParameters["userId"].Should().Be("42");
        result.ResolvedParameters["orderId"].Should().Be("ord-777");
        result.ResolvedParameters["postId"].Should().Be("abc 123/xyz");
    }

    [Fact]
    public async Task HandleAsync_Should_LeaveParamUnresolved_WhenExamplesJsonMalformed()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        SetupRepositories(
            projects: new List<Project>
            {
                new() { Id = projectId, OwnerId = ownerId, Name = "P", Status = ProjectStatus.Active },
            },
            specs: new List<ApiSpecification>
            {
                new() { Id = specId, ProjectId = projectId, Name = "Spec", ParseStatus = ParseStatus.Success, SourceType = SourceType.Manual },
            },
            endpoints: new List<ApiEndpoint>
            {
                new() { Id = endpointId, ApiSpecId = specId, HttpMethod = ClassifiedAds.Modules.ApiDocumentation.Entities.HttpMethod.GET, Path = "/api/users/{userId}/orders/{orderId}" },
            },
            parameters: new List<EndpointParameter>
            {
                new() { Id = Guid.NewGuid(), EndpointId = endpointId, Name = "userId", Location = ParameterLocation.Path, DefaultValue = "42", DataType = "integer", IsRequired = true },
                new() { Id = Guid.NewGuid(), EndpointId = endpointId, Name = "orderId", Location = ParameterLocation.Path, Examples = "{bad json}", DataType = "string", IsRequired = true },
            });

        var query = new GetResolvedUrlQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            OwnerId = ownerId,
            ParameterValues = null,
        };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.IsFullyResolved.Should().BeFalse();
        result.ResolvedUrl.Should().BeNull();
        result.UnresolvedParameters.Should().Contain("orderId");
        result.ResolvedParameters.Should().ContainKey("userId").WhoseValue.Should().Be("42");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenProjectOwnerMismatch()
    {
        // Arrange
        var actualOwnerId = Guid.NewGuid();
        var queryOwnerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        SetupRepositories(
            projects: new List<Project>
            {
                new() { Id = projectId, OwnerId = actualOwnerId, Name = "P", Status = ProjectStatus.Active },
            },
            specs: new List<ApiSpecification>
            {
                new() { Id = specId, ProjectId = projectId, Name = "Spec", ParseStatus = ParseStatus.Success, SourceType = SourceType.Manual },
            },
            endpoints: new List<ApiEndpoint>
            {
                new() { Id = endpointId, ApiSpecId = specId, HttpMethod = ClassifiedAds.Modules.ApiDocumentation.Entities.HttpMethod.GET, Path = "/api/users/{userId}" },
            },
            parameters: new List<EndpointParameter>());

        var query = new GetResolvedUrlQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            OwnerId = queryOwnerId,
            ParameterValues = new Dictionary<string, string>(),
        };

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private void SetupRepositories(
        List<Project> projects,
        List<ApiSpecification> specs,
        List<ApiEndpoint> endpoints,
        List<EndpointParameter> parameters)
    {
        _projectRepoMock.Setup(x => x.GetQueryableSet()).Returns(projects.AsQueryable());
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync((IQueryable<Project> query) => query.FirstOrDefault());

        _specRepoMock.Setup(x => x.GetQueryableSet()).Returns(specs.AsQueryable());
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync((IQueryable<ApiSpecification> query) => query.FirstOrDefault());

        _endpointRepoMock.Setup(x => x.GetQueryableSet()).Returns(endpoints.AsQueryable());
        _endpointRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiEndpoint>>()))
            .ReturnsAsync((IQueryable<ApiEndpoint> query) => query.FirstOrDefault());

        _parameterRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new TestAsyncEnumerable<EndpointParameter>(parameters));
    }
}

public class GetPathParamMutationsQueryHandlerTests
{
    private readonly Mock<IRepository<Project, Guid>> _projectRepoMock;
    private readonly Mock<IRepository<ApiSpecification, Guid>> _specRepoMock;
    private readonly Mock<IRepository<ApiEndpoint, Guid>> _endpointRepoMock;
    private readonly Mock<IRepository<EndpointParameter, Guid>> _parameterRepoMock;
    private readonly GetPathParamMutationsQueryHandler _handler;

    public GetPathParamMutationsQueryHandlerTests()
    {
        _projectRepoMock = new Mock<IRepository<Project, Guid>>();
        _specRepoMock = new Mock<IRepository<ApiSpecification, Guid>>();
        _endpointRepoMock = new Mock<IRepository<ApiEndpoint, Guid>>();
        _parameterRepoMock = new Mock<IRepository<EndpointParameter, Guid>>();

        _handler = new GetPathParamMutationsQueryHandler(
            _projectRepoMock.Object,
            _specRepoMock.Object,
            _endpointRepoMock.Object,
            _parameterRepoMock.Object,
            new PathParameterTemplateService());
    }

    [Fact]
    public async Task HandleAsync_Should_GenerateMutationsWithResolvedUrls_ForEachPathParam()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        const string defaultOrderId = "550e8400-e29b-41d4-a716-446655440000";

        SetupRepositories(
            projects: new List<Project>
            {
                new() { Id = projectId, OwnerId = ownerId, Name = "P", Status = ProjectStatus.Active },
            },
            specs: new List<ApiSpecification>
            {
                new() { Id = specId, ProjectId = projectId, Name = "Spec", ParseStatus = ParseStatus.Success, SourceType = SourceType.Manual },
            },
            endpoints: new List<ApiEndpoint>
            {
                new() { Id = endpointId, ApiSpecId = specId, HttpMethod = ClassifiedAds.Modules.ApiDocumentation.Entities.HttpMethod.GET, Path = "/api/users/{userId}/orders/{orderId}" },
            },
            parameters: new List<EndpointParameter>
            {
                new() { Id = Guid.NewGuid(), EndpointId = endpointId, Name = "userId", Location = ParameterLocation.Path, DataType = "integer", Format = "int32", DefaultValue = "123", IsRequired = true },
                new() { Id = Guid.NewGuid(), EndpointId = endpointId, Name = "orderId", Location = ParameterLocation.Path, DataType = "string", Format = "uuid", DefaultValue = defaultOrderId, IsRequired = true },
            });

        var query = new GetPathParamMutationsQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            OwnerId = ownerId,
        };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.EndpointId.Should().Be(endpointId);
        result.TemplatePath.Should().Be("/api/users/{userId}/orders/{orderId}");
        result.ParameterMutations.Should().HaveCount(2);
        result.TotalMutations.Should().Be(result.ParameterMutations.Sum(g => g.Mutations.Count));

        var userIdGroup = result.ParameterMutations.Single(g => g.ParameterName == "userId");
        userIdGroup.Mutations.Should().NotBeEmpty();
        userIdGroup.Mutations.Should().Contain(m => m.MutationType == "empty" && m.ResolvedUrl == null);
        userIdGroup.Mutations
            .Where(m => m.MutationType != "empty")
            .Should()
            .OnlyContain(m => !string.IsNullOrWhiteSpace(m.ResolvedUrl));
        userIdGroup.Mutations.Should().Contain(m =>
            m.MutationType == "boundary_max_int32" &&
            m.ResolvedUrl == $"/api/users/2147483647/orders/{defaultOrderId}");

        var orderIdGroup = result.ParameterMutations.Single(g => g.ParameterName == "orderId");
        orderIdGroup.Mutations.Should().NotBeEmpty();
        orderIdGroup.Mutations.Should().Contain(m =>
            m.MutationType == "invalidFormat" &&
            m.ResolvedUrl == "/api/users/123/orders/not-a-uuid");
    }

    [Fact]
    public async Task HandleAsync_Should_ReturnEmpty_WhenEndpointHasNoPathParameters()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        SetupRepositories(
            projects: new List<Project>
            {
                new() { Id = projectId, OwnerId = ownerId, Name = "P", Status = ProjectStatus.Active },
            },
            specs: new List<ApiSpecification>
            {
                new() { Id = specId, ProjectId = projectId, Name = "Spec", ParseStatus = ParseStatus.Success, SourceType = SourceType.Manual },
            },
            endpoints: new List<ApiEndpoint>
            {
                new() { Id = endpointId, ApiSpecId = specId, HttpMethod = ClassifiedAds.Modules.ApiDocumentation.Entities.HttpMethod.GET, Path = "/api/health" },
            },
            parameters: new List<EndpointParameter>
            {
                new() { Id = Guid.NewGuid(), EndpointId = endpointId, Name = "search", Location = ParameterLocation.Query, DataType = "string" },
            });

        var query = new GetPathParamMutationsQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            OwnerId = ownerId,
        };

        // Act
        var result = await _handler.HandleAsync(query);

        // Assert
        result.EndpointId.Should().Be(endpointId);
        result.TemplatePath.Should().Be("/api/health");
        result.TotalMutations.Should().Be(0);
        result.ParameterMutations.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenEndpointNotBelongToSpec()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var differentSpecId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        SetupRepositories(
            projects: new List<Project>
            {
                new() { Id = projectId, OwnerId = ownerId, Name = "P", Status = ProjectStatus.Active },
            },
            specs: new List<ApiSpecification>
            {
                new() { Id = specId, ProjectId = projectId, Name = "Spec", ParseStatus = ParseStatus.Success, SourceType = SourceType.Manual },
            },
            endpoints: new List<ApiEndpoint>
            {
                new() { Id = endpointId, ApiSpecId = differentSpecId, HttpMethod = ClassifiedAds.Modules.ApiDocumentation.Entities.HttpMethod.GET, Path = "/api/users/{userId}" },
            },
            parameters: new List<EndpointParameter>());

        var query = new GetPathParamMutationsQuery
        {
            ProjectId = projectId,
            SpecId = specId,
            EndpointId = endpointId,
            OwnerId = ownerId,
        };

        // Act
        var act = () => _handler.HandleAsync(query);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private void SetupRepositories(
        List<Project> projects,
        List<ApiSpecification> specs,
        List<ApiEndpoint> endpoints,
        List<EndpointParameter> parameters)
    {
        _projectRepoMock.Setup(x => x.GetQueryableSet()).Returns(projects.AsQueryable());
        _projectRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<Project>>()))
            .ReturnsAsync((IQueryable<Project> query) => query.FirstOrDefault());

        _specRepoMock.Setup(x => x.GetQueryableSet()).Returns(specs.AsQueryable());
        _specRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiSpecification>>()))
            .ReturnsAsync((IQueryable<ApiSpecification> query) => query.FirstOrDefault());

        _endpointRepoMock.Setup(x => x.GetQueryableSet()).Returns(endpoints.AsQueryable());
        _endpointRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ApiEndpoint>>()))
            .ReturnsAsync((IQueryable<ApiEndpoint> query) => query.FirstOrDefault());

        _parameterRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new TestAsyncEnumerable<EndpointParameter>(parameters));
    }
}
