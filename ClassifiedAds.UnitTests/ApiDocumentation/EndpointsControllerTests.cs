using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.ApiDocumentation.Commands;
using ClassifiedAds.Modules.ApiDocumentation.Controllers;
using ClassifiedAds.Modules.ApiDocumentation.Models;
using ClassifiedAds.Modules.ApiDocumentation.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.ApiDocumentation;

public class EndpointsControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<EndpointsController>> _loggerMock;
    private readonly Mock<ICommandHandler<AddUpdateEndpointCommand>> _addUpdateHandlerMock;
    private readonly Mock<ICommandHandler<DeleteEndpointCommand>> _deleteHandlerMock;
    private readonly Mock<IQueryHandler<GetEndpointsQuery, List<EndpointModel>>> _getEndpointsHandlerMock;
    private readonly Mock<IQueryHandler<GetEndpointQuery, EndpointDetailModel>> _getEndpointHandlerMock;
    private readonly EndpointsController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public EndpointsControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<EndpointsController>>();
        _addUpdateHandlerMock = new Mock<ICommandHandler<AddUpdateEndpointCommand>>();
        _deleteHandlerMock = new Mock<ICommandHandler<DeleteEndpointCommand>>();
        _getEndpointsHandlerMock = new Mock<IQueryHandler<GetEndpointsQuery, List<EndpointModel>>>();
        _getEndpointHandlerMock = new Mock<IQueryHandler<GetEndpointQuery, EndpointDetailModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<AddUpdateEndpointCommand>))).Returns(_addUpdateHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<DeleteEndpointCommand>))).Returns(_deleteHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetEndpointsQuery, List<EndpointModel>>))).Returns(_getEndpointsHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetEndpointQuery, EndpointDetailModel>))).Returns(_getEndpointHandlerMock.Object);

        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new EndpointsController(dispatcher, _currentUserMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task Get_Should_ReturnOkWithEndpoints()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpoints = new List<EndpointModel>
        {
            new() { Id = Guid.NewGuid(), ApiSpecId = specId, HttpMethod = "GET", Path = "/users", Summary = "Get users" },
            new() { Id = Guid.NewGuid(), ApiSpecId = specId, HttpMethod = "POST", Path = "/users", Summary = "Create user" },
        };

        _getEndpointsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(endpoints);

        var result = await _controller.Get(projectId, specId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeAssignableTo<List<EndpointModel>>().Subject;
        payload.Should().HaveCount(2);
        payload[0].Path.Should().Be("/users");
    }

    [Fact]
    public async Task Get_Should_PassProjectSpecAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        GetEndpointsQuery capturedQuery = null!;

        _getEndpointsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetEndpointsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<EndpointModel>());

        await _controller.Get(projectId, specId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Get_Should_ReturnOkWithEmptyEndpointList()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _getEndpointsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EndpointModel>());

        var result = await _controller.Get(projectId, specId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<EndpointModel>>().Subject.Should().BeEmpty();
    }

    [Fact]
    public async Task Get_Should_ReturnEndpointMetadataFields()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();

        _getEndpointsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<EndpointModel>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    ApiSpecId = specId,
                    HttpMethod = "PATCH",
                    Path = "/users/{id}",
                    Description = "Update user",
                    Tags = "[\"users\"]",
                    IsDeprecated = false,
                },
            });

        var result = await _controller.Get(projectId, specId);

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<List<EndpointModel>>().Subject;
        payload[0].HttpMethod.Should().Be("PATCH");
        payload[0].Description.Should().Be("Update user");
    }

    [Fact]
    public async Task Get_Should_ThrowNotFoundException_WhenSpecMissing()
    {
        _getEndpointsHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointsQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Specification not found"));

        var act = () => _controller.Get(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Specification not found*");
    }

    [Fact]
    public async Task GetById_Should_ReturnOkWithEndpointDetail()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        var result = await _controller.GetById(projectId, specId, endpointId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<EndpointDetailModel>().Subject;
        payload.Id.Should().Be(endpointId);
        payload.Parameters.Should().ContainSingle();
        payload.Responses.Should().ContainSingle();
    }

    [Fact]
    public async Task GetById_Should_PassRouteIdentifiersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        GetEndpointQuery capturedQuery = null!;

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetEndpointQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        await _controller.GetById(projectId, specId, endpointId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.EndpointId.Should().Be(endpointId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetById_Should_ReturnSecurityAndResolvedUrlData()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        var result = await _controller.GetById(projectId, specId, endpointId);

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<EndpointDetailModel>().Subject;
        payload.ResolvedUrl.Should().Contain("/users/");
        payload.SecurityRequirements.Should().ContainSingle();
    }

    [Fact]
    public async Task GetById_Should_ThrowNotFoundException_WhenEndpointMissing()
    {
        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Endpoint not found"));

        var act = () => _controller.GetById(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Endpoint not found*");
    }

    [Fact]
    public async Task Post_Should_ReturnCreatedWithCreatedEndpoint()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var model = CreateUpdateModel();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateEndpointCommand, CancellationToken>((command, _) => command.SavedEndpointId = endpointId)
            .Returns(Task.CompletedTask);

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        var result = await _controller.Post(projectId, specId, model);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/projects/{projectId}/specifications/{specId}/endpoints/{endpointId}");
        createdResult.Value.Should().BeOfType<EndpointDetailModel>().Subject.Id.Should().Be(endpointId);
    }

    [Fact]
    public async Task Post_Should_MapBodyIntoCreateCommand()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var model = CreateUpdateModel();
        AddUpdateEndpointCommand capturedCommand = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateEndpointCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedEndpointId = endpointId;
            })
            .Returns(Task.CompletedTask);

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        await _controller.Post(projectId, specId, model);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SpecId.Should().Be(specId);
        capturedCommand.EndpointId.Should().BeNull();
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Model.Should().BeSameAs(model);
    }

    [Fact]
    public async Task Post_Should_RequestSavedEndpointBySavedEndpointId()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        GetEndpointQuery capturedQuery = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateEndpointCommand, CancellationToken>((command, _) => command.SavedEndpointId = endpointId)
            .Returns(Task.CompletedTask);

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetEndpointQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        await _controller.Post(projectId, specId, CreateUpdateModel());

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.EndpointId.Should().Be(endpointId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Post_Should_PreserveComplexEndpointBodyFields()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var model = CreateUpdateModel();
        AddUpdateEndpointCommand capturedCommand = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateEndpointCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedEndpointId = endpointId;
            })
            .Returns(Task.CompletedTask);

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        await _controller.Post(projectId, specId, model);

        capturedCommand.Model.Tags.Should().Contain("users");
        capturedCommand.Model.Parameters.Should().ContainSingle();
        capturedCommand.Model.Responses.Should().ContainSingle();
    }

    [Fact]
    public async Task Post_Should_ThrowValidationException_WhenCreateFails()
    {
        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Endpoint path already exists"));

        var act = () => _controller.Post(Guid.NewGuid(), Guid.NewGuid(), CreateUpdateModel());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Endpoint path already exists*");
    }

    [Fact]
    public async Task Put_Should_ReturnOkWithUpdatedEndpoint()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var model = CreateUpdateModel();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateEndpointCommand, CancellationToken>((command, _) => command.SavedEndpointId = endpointId)
            .Returns(Task.CompletedTask);

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        var result = await _controller.Put(projectId, specId, endpointId, model);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<EndpointDetailModel>().Subject.Id.Should().Be(endpointId);
    }

    [Fact]
    public async Task Put_Should_DispatchUpdateCommandWithEndpointId()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        AddUpdateEndpointCommand capturedCommand = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateEndpointCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedEndpointId = endpointId;
            })
            .Returns(Task.CompletedTask);

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        await _controller.Put(projectId, specId, endpointId, CreateUpdateModel());

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SpecId.Should().Be(specId);
        capturedCommand.EndpointId.Should().Be(endpointId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Put_Should_RequestUpdatedEndpointBySavedEndpointId()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        GetEndpointQuery capturedQuery = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateEndpointCommand, CancellationToken>((command, _) => command.SavedEndpointId = endpointId)
            .Returns(Task.CompletedTask);

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetEndpointQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        await _controller.Put(projectId, specId, endpointId, CreateUpdateModel());

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.SpecId.Should().Be(specId);
        capturedQuery.EndpointId.Should().Be(endpointId);
        capturedQuery.OwnerId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Put_Should_KeepUpdatedMethodAndPathValues()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var model = CreateUpdateModel();
        model.HttpMethod = "PATCH";
        model.Path = "/users/{id}";
        AddUpdateEndpointCommand capturedCommand = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateEndpointCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedEndpointId = endpointId;
            })
            .Returns(Task.CompletedTask);

        _getEndpointHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetEndpointQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateDetailModel(projectId, specId, endpointId));

        await _controller.Put(projectId, specId, endpointId, model);

        capturedCommand.Model.HttpMethod.Should().Be("PATCH");
        capturedCommand.Model.Path.Should().Be("/users/{id}");
    }

    [Fact]
    public async Task Put_Should_ThrowNotFoundException_WhenUpdateFails()
    {
        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Endpoint not found"));

        var act = () => _controller.Put(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CreateUpdateModel());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Endpoint not found*");
    }

    [Fact]
    public async Task Delete_Should_ReturnNoContent()
    {
        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_Should_DispatchDeleteCommandWithIdentifiers()
    {
        var projectId = Guid.NewGuid();
        var specId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        DeleteEndpointCommand capturedCommand = null!;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteEndpointCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Delete(projectId, specId, endpointId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.SpecId.Should().Be(specId);
        capturedCommand.EndpointId.Should().Be(endpointId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Delete_Should_ForwardExactEndpointId()
    {
        var endpointId = Guid.NewGuid();
        DeleteEndpointCommand capturedCommand = null!;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteEndpointCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteEndpointCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Delete(Guid.NewGuid(), Guid.NewGuid(), endpointId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.EndpointId.Should().Be(endpointId);
    }

    [Fact]
    public async Task Delete_Should_ThrowNotFoundException_WhenDeleteFails()
    {
        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteEndpointCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Endpoint not found"));

        var act = () => _controller.Delete(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Endpoint not found*");
    }

    private static CreateUpdateEndpointModel CreateUpdateModel()
    {
        return new CreateUpdateEndpointModel
        {
            HttpMethod = "POST",
            Path = "/users",
            OperationId = "createUser",
            Summary = "Create user",
            Description = "Creates a user record",
            Tags = new List<string> { "users" },
            IsDeprecated = false,
            Parameters = new List<ManualParameterDefinition>
            {
                new()
                {
                    Name = "tenantId",
                    Location = "header",
                    DataType = "string",
                    IsRequired = true,
                },
            },
            Responses = new List<ManualResponseDefinition>
            {
                new()
                {
                    StatusCode = 201,
                    Description = "Created",
                },
            },
        };
    }

    private static EndpointDetailModel CreateDetailModel(Guid projectId, Guid specId, Guid endpointId)
    {
        return new EndpointDetailModel
        {
            Id = endpointId,
            ApiSpecId = specId,
            HttpMethod = "GET",
            Path = "/users/{id}",
            OperationId = "getUser",
            Summary = "Get user",
            Description = "Get a user by id",
            ResolvedUrl = $"https://api.example.com/projects/{projectId}/users/42",
            Parameters = new List<ParameterModel>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Name = "id",
                    Location = "path",
                    DataType = "string",
                    IsRequired = true,
                },
            },
            Responses = new List<ResponseModel>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    StatusCode = 200,
                    Description = "Success",
                },
            },
            SecurityRequirements = new List<SecurityReqModel>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SecurityType = "bearer",
                    SchemeName = "Bearer",
                    Scopes = "read:users",
                },
            },
        };
    }
}
