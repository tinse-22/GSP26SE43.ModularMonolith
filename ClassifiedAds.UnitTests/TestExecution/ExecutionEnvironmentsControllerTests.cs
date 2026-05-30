using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestExecution.Commands;
using ClassifiedAds.Modules.TestExecution.Controllers;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Models.Requests;
using ClassifiedAds.Modules.TestExecution.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class ExecutionEnvironmentsControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<ExecutionEnvironmentsController>> _loggerMock;
    private readonly Mock<ICommandHandler<AddUpdateExecutionEnvironmentCommand>> _addUpdateHandlerMock;
    private readonly Mock<ICommandHandler<DeleteExecutionEnvironmentCommand>> _deleteHandlerMock;
    private readonly Mock<IQueryHandler<GetExecutionEnvironmentsQuery, List<ExecutionEnvironmentModel>>> _getAllHandlerMock;
    private readonly Mock<IQueryHandler<GetExecutionEnvironmentQuery, ExecutionEnvironmentModel>> _getByIdHandlerMock;
    private readonly ExecutionEnvironmentsController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public ExecutionEnvironmentsControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<ExecutionEnvironmentsController>>();
        _addUpdateHandlerMock = new Mock<ICommandHandler<AddUpdateExecutionEnvironmentCommand>>();
        _deleteHandlerMock = new Mock<ICommandHandler<DeleteExecutionEnvironmentCommand>>();
        _getAllHandlerMock = new Mock<IQueryHandler<GetExecutionEnvironmentsQuery, List<ExecutionEnvironmentModel>>>();
        _getByIdHandlerMock = new Mock<IQueryHandler<GetExecutionEnvironmentQuery, ExecutionEnvironmentModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<AddUpdateExecutionEnvironmentCommand>)))
            .Returns(_addUpdateHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<DeleteExecutionEnvironmentCommand>)))
            .Returns(_deleteHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetExecutionEnvironmentsQuery, List<ExecutionEnvironmentModel>>)))
            .Returns(_getAllHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetExecutionEnvironmentQuery, ExecutionEnvironmentModel>)))
            .Returns(_getByIdHandlerMock.Object);

        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new ExecutionEnvironmentsController(dispatcher, _currentUserMock.Object, _loggerMock.Object);
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GetAll_Should_ReturnOkWithEnvironmentList()
    {
        var projectId = Guid.NewGuid();
        var expected = new List<ExecutionEnvironmentModel>
        {
            CreateEnvironmentModel(projectId, Guid.NewGuid(), "QA", true),
            CreateEnvironmentModel(projectId, Guid.NewGuid(), "Staging", false),
        };

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetExecutionEnvironmentsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.GetAll(projectId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeOfType<List<ExecutionEnvironmentModel>>().Subject;
        payload.Should().HaveCount(2);
        payload[0].IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_Should_MapProjectIdAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        GetExecutionEnvironmentsQuery capturedQuery = null!;

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetExecutionEnvironmentsQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetExecutionEnvironmentsQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<ExecutionEnvironmentModel>());

        await _controller.GetAll(projectId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetById_Should_ReturnOkWithEnvironmentPayload()
    {
        var projectId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetExecutionEnvironmentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateEnvironmentModel(projectId, environmentId, "QA", true));

        var result = await _controller.GetById(projectId, environmentId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<ExecutionEnvironmentModel>().Subject.Id.Should().Be(environmentId);
    }

    [Fact]
    public async Task GetById_Should_MapIdentifiersAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        GetExecutionEnvironmentQuery capturedQuery = null!;

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetExecutionEnvironmentQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetExecutionEnvironmentQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateEnvironmentModel(projectId, environmentId, "Dev", false));

        await _controller.GetById(projectId, environmentId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ProjectId.Should().Be(projectId);
        capturedQuery.EnvironmentId.Should().Be(environmentId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetById_Should_PropagateNotFoundException()
    {
        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetExecutionEnvironmentQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("ENVIRONMENT_NOT_FOUND"));

        var act = () => _controller.GetById(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*ENVIRONMENT_NOT_FOUND*");
    }

    [Fact]
    public async Task Create_Should_ReturnCreatedWithLocationAndPayload()
    {
        var projectId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        var expected = CreateEnvironmentModel(projectId, environmentId, "QA", true);

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateExecutionEnvironmentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateExecutionEnvironmentCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.Create(projectId, new CreateExecutionEnvironmentRequest
        {
            Name = "QA",
            BaseUrl = "https://qa.example.com",
            Variables = new Dictionary<string, string> { ["region"] = "apac" },
            Headers = new Dictionary<string, string> { ["x-env"] = "qa" },
            AuthConfig = CreateAuthConfig(),
            IsDefault = true,
        });

        var created = result.Result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().Be($"/api/projects/{projectId}/execution-environments/{environmentId}");
        created.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Create_Should_MapBodyFieldsAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        AddUpdateExecutionEnvironmentCommand capturedCommand = null!;
        var variables = new Dictionary<string, string> { ["tenant"] = "blue" };
        var headers = new Dictionary<string, string> { ["Authorization"] = "Bearer token" };
        var authConfig = CreateAuthConfig();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateExecutionEnvironmentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateExecutionEnvironmentCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateEnvironmentModel(projectId, Guid.NewGuid(), "Staging", false);
            })
            .Returns(Task.CompletedTask);

        await _controller.Create(projectId, new CreateExecutionEnvironmentRequest
        {
            Name = "Staging",
            BaseUrl = "https://staging.example.com",
            Variables = variables,
            Headers = headers,
            AuthConfig = authConfig,
            IsDefault = false,
        });

        capturedCommand.Should().NotBeNull();
        capturedCommand.EnvironmentId.Should().BeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.Name.Should().Be("Staging");
        capturedCommand.BaseUrl.Should().Be("https://staging.example.com");
        capturedCommand.Variables.Should().BeSameAs(variables);
        capturedCommand.Headers.Should().BeSameAs(headers);
        capturedCommand.AuthConfig.Should().BeSameAs(authConfig);
        capturedCommand.IsDefault.Should().BeFalse();
    }

    [Fact]
    public async Task Create_Should_PropagateConflictException()
    {
        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateExecutionEnvironmentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("DEFAULT_ENVIRONMENT_CONFLICT", "conflict"));

        var act = () => _controller.Create(Guid.NewGuid(), new CreateExecutionEnvironmentRequest
        {
            Name = "QA",
            BaseUrl = "https://qa.example.com",
        });

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("DEFAULT_ENVIRONMENT_CONFLICT");
    }

    [Fact]
    public async Task Update_Should_ReturnOkWithUpdatedEnvironment()
    {
        var projectId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        var expected = CreateEnvironmentModel(projectId, environmentId, "Prod", false);

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateExecutionEnvironmentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateExecutionEnvironmentCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.Update(projectId, environmentId, new UpdateExecutionEnvironmentRequest
        {
            RowVersion = "cm93VmVyc2lvbg==",
            Name = "Prod",
            BaseUrl = "https://prod.example.com",
        });

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Update_Should_MapRouteBodyAndRowVersion()
    {
        var projectId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        AddUpdateExecutionEnvironmentCommand capturedCommand = null!;
        var variables = new Dictionary<string, string> { ["apiVersion"] = "v1" };
        var headers = new Dictionary<string, string> { ["x-correlation-id"] = "abc" };
        var authConfig = new ExecutionAuthConfigModel
        {
            AuthType = AuthType.ApiKey,
            ApiKeyName = "x-api-key",
            ApiKeyValue = "secret",
            ApiKeyLocation = ApiKeyLocation.Header,
        };

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateExecutionEnvironmentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdateExecutionEnvironmentCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateEnvironmentModel(projectId, environmentId, "Prod", true);
            })
            .Returns(Task.CompletedTask);

        await _controller.Update(projectId, environmentId, new UpdateExecutionEnvironmentRequest
        {
            RowVersion = "dmVyc2lvbi0y",
            Name = "Prod",
            BaseUrl = "https://prod.example.com",
            Variables = variables,
            Headers = headers,
            AuthConfig = authConfig,
            IsDefault = true,
        });

        capturedCommand.Should().NotBeNull();
        capturedCommand.EnvironmentId.Should().Be(environmentId);
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.RowVersion.Should().Be("dmVyc2lvbi0y");
        capturedCommand.Name.Should().Be("Prod");
        capturedCommand.BaseUrl.Should().Be("https://prod.example.com");
        capturedCommand.Variables.Should().BeSameAs(variables);
        capturedCommand.Headers.Should().BeSameAs(headers);
        capturedCommand.AuthConfig.Should().BeSameAs(authConfig);
        capturedCommand.IsDefault.Should().BeTrue();
    }

    [Fact]
    public async Task Update_Should_PropagateNotFoundException()
    {
        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdateExecutionEnvironmentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("ENVIRONMENT_NOT_FOUND"));

        var act = () => _controller.Update(Guid.NewGuid(), Guid.NewGuid(), new UpdateExecutionEnvironmentRequest
        {
            RowVersion = "dmVyc2lvbg==",
            Name = "QA",
            BaseUrl = "https://qa.example.com",
        });

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*ENVIRONMENT_NOT_FOUND*");
    }

    [Fact]
    public async Task Delete_Should_ReturnNoContent()
    {
        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteExecutionEnvironmentCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(Guid.NewGuid(), Guid.NewGuid(), "cm93VmVyc2lvbg==");

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task Delete_Should_MapRouteQueryAndCurrentUser()
    {
        var projectId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        DeleteExecutionEnvironmentCommand capturedCommand = null!;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteExecutionEnvironmentCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteExecutionEnvironmentCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Delete(projectId, environmentId, "cm93LXYz");

        capturedCommand.Should().NotBeNull();
        capturedCommand.ProjectId.Should().Be(projectId);
        capturedCommand.EnvironmentId.Should().Be(environmentId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.RowVersion.Should().Be("cm93LXYz");
    }

    [Fact]
    public async Task Delete_Should_PropagateConflictException()
    {
        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteExecutionEnvironmentCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConflictException("CONCURRENCY_CONFLICT", "row version mismatch"));

        var act = () => _controller.Delete(Guid.NewGuid(), Guid.NewGuid(), "dmVyc2lvbg==");

        var ex = await act.Should().ThrowAsync<ConflictException>();
        ex.Which.ReasonCode.Should().Be("CONCURRENCY_CONFLICT");
    }

    private static ExecutionEnvironmentModel CreateEnvironmentModel(
        Guid projectId,
        Guid environmentId,
        string name,
        bool isDefault)
    {
        return new ExecutionEnvironmentModel
        {
            Id = environmentId,
            ProjectId = projectId,
            Name = name,
            BaseUrl = $"https://{name.ToLowerInvariant()}.example.com",
            Variables = new Dictionary<string, string> { ["region"] = "apac" },
            Headers = new Dictionary<string, string> { ["x-env"] = name.ToLowerInvariant() },
            AuthConfig = CreateAuthConfig(),
            IsDefault = isDefault,
            CreatedDateTime = DateTimeOffset.UtcNow.AddMinutes(-10),
            UpdatedDateTime = DateTimeOffset.UtcNow,
            RowVersion = "cm93VmVyc2lvbg==",
        };
    }

    private static ExecutionAuthConfigModel CreateAuthConfig()
    {
        return new ExecutionAuthConfigModel
        {
            AuthType = AuthType.BearerToken,
            HeaderName = "Authorization",
            Token = "secret-token",
        };
    }
}
