using ClassifiedAds.Application;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Controllers;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class PlansControllerTests
{
    private readonly Mock<ILogger<PlansController>> _loggerMock;

    // Mock handlers (Dispatcher resolves these from IServiceProvider)
    private readonly Mock<ICommandHandler<AddUpdatePlanCommand>> _addUpdateHandlerMock;
    private readonly Mock<ICommandHandler<DeletePlanCommand>> _deleteHandlerMock;
    private readonly Mock<IQueryHandler<GetPlansQuery, List<PlanModel>>> _getPlansHandlerMock;
    private readonly Mock<IQueryHandler<GetPlanQuery, PlanModel>> _getPlanHandlerMock;

    private readonly Dispatcher _dispatcher;
    private readonly PlansController _controller;

    public PlansControllerTests()
    {
        _loggerMock = new Mock<ILogger<PlansController>>();

        _addUpdateHandlerMock = new Mock<ICommandHandler<AddUpdatePlanCommand>>();
        _deleteHandlerMock = new Mock<ICommandHandler<DeletePlanCommand>>();
        _getPlansHandlerMock = new Mock<IQueryHandler<GetPlansQuery, List<PlanModel>>>();
        _getPlanHandlerMock = new Mock<IQueryHandler<GetPlanQuery, PlanModel>>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<AddUpdatePlanCommand>)))
            .Returns(_addUpdateHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(ICommandHandler<DeletePlanCommand>)))
            .Returns(_deleteHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetPlansQuery, List<PlanModel>>)))
            .Returns(_getPlansHandlerMock.Object);
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetPlanQuery, PlanModel>)))
            .Returns(_getPlanHandlerMock.Object);

        _dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new PlansController(_dispatcher, _loggerMock.Object);
    }

    #region GET /api/plans Tests

    [Fact]
    public async Task Get_Should_ReturnOkWithPlans()
    {
        // Arrange
        var plans = new List<PlanModel>
        {
            new PlanModel { Id = Guid.NewGuid(), Name = "Free", DisplayName = "Free Plan" },
            new PlanModel { Id = Guid.NewGuid(), Name = "Pro", DisplayName = "Pro Plan" },
        };

        _getPlansHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlansQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);

        // Act
        var result = await _controller.Get(null, null);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedPlans = okResult.Value.Should().BeAssignableTo<List<PlanModel>>().Subject;
        returnedPlans.Should().HaveCount(2);
    }

    [Fact]
    public async Task Get_Should_PassFilterParameters()
    {
        // Arrange
        GetPlansQuery capturedQuery = null!;
        _getPlansHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlansQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetPlansQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(new List<PlanModel>());

        // Act
        await _controller.Get(isActive: true, search: "Pro");

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery.IsActive.Should().BeTrue();
        capturedQuery.Search.Should().Be("Pro");
    }

    #endregion

    #region GET /api/plans/{id} Tests

    [Fact]
    public async Task GetById_Should_ReturnOkWithPlan()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var plan = new PlanModel { Id = planId, Name = "Pro", DisplayName = "Pro Plan" };

        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plan);

        // Act
        var result = await _controller.Get(planId);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedPlan = okResult.Value.Should().BeOfType<PlanModel>().Subject;
        returnedPlan.Id.Should().Be(planId);
    }

    [Fact]
    public async Task GetById_Should_SetThrowNotFoundIfNull()
    {
        // Arrange
        GetPlanQuery capturedQuery = null!;
        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetPlanQuery, CancellationToken>((q, _) => capturedQuery = q)
            .ReturnsAsync(new PlanModel());

        // Act
        await _controller.Get(Guid.NewGuid());

        // Assert
        capturedQuery.Should().NotBeNull();
        capturedQuery.ThrowNotFoundIfNull.Should().BeTrue();
    }

    #endregion

    #region POST /api/plans Tests

    [Fact]
    public async Task Post_Should_ReturnCreatedWithPlan()
    {
        // Arrange
        var savedPlanId = Guid.NewGuid();
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro",
            DisplayName = "Pro Plan",
        };

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdatePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdatePlanCommand, CancellationToken>((cmd, _) => cmd.SavedPlanId = savedPlanId)
            .Returns(Task.CompletedTask);

        var expectedPlan = new PlanModel { Id = savedPlanId, Name = "Pro", DisplayName = "Pro Plan" };
        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedPlan);

        // Act
        var result = await _controller.Post(model);

        // Assert
        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Contain(savedPlanId.ToString());
        var returnedPlan = createdResult.Value.Should().BeOfType<PlanModel>().Subject;
        returnedPlan.Id.Should().Be(savedPlanId);
    }

    #endregion

    #region PUT /api/plans/{id} Tests

    [Fact]
    public async Task Put_Should_ReturnOkWithUpdatedPlan()
    {
        // Arrange
        var planId = Guid.NewGuid();
        var model = new CreateUpdatePlanModel
        {
            Name = "Pro Updated",
            DisplayName = "Pro Plan Updated",
        };

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdatePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdatePlanCommand, CancellationToken>((cmd, _) => cmd.SavedPlanId = planId)
            .Returns(Task.CompletedTask);

        var updatedPlan = new PlanModel { Id = planId, Name = "Pro Updated" };
        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updatedPlan);

        // Act
        var result = await _controller.Put(planId, model);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returnedPlan = okResult.Value.Should().BeOfType<PlanModel>().Subject;
        returnedPlan.Id.Should().Be(planId);
    }

    [Fact]
    public async Task Put_Should_PassPlanIdToCommand()
    {
        // Arrange
        var planId = Guid.NewGuid();
        AddUpdatePlanCommand capturedCommand = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdatePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdatePlanCommand, CancellationToken>((cmd, _) =>
            {
                capturedCommand = cmd;
                cmd.SavedPlanId = planId;
            })
            .Returns(Task.CompletedTask);

        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlanModel { Id = planId });

        var model = new CreateUpdatePlanModel { Name = "Pro", DisplayName = "Pro Plan" };

        // Act
        await _controller.Put(planId, model);

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand.PlanId.Should().Be(planId);
    }

    #endregion

    #region DELETE /api/plans/{id} Tests

    [Fact]
    public async Task Delete_Should_ReturnOk()
    {
        // Arrange
        var planId = Guid.NewGuid();

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeletePlanCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _controller.Delete(planId);

        // Assert
        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task Delete_Should_DispatchDeleteCommand()
    {
        // Arrange
        var planId = Guid.NewGuid();
        DeletePlanCommand capturedCommand = null!;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeletePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeletePlanCommand, CancellationToken>((cmd, _) => capturedCommand = cmd)
            .Returns(Task.CompletedTask);

        // Act
        await _controller.Delete(planId);

        // Assert
        capturedCommand.Should().NotBeNull();
        capturedCommand.PlanId.Should().Be(planId);
    }

    #endregion
}
