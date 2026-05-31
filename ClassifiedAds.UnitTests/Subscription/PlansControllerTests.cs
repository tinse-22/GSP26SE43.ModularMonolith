using ClassifiedAds.Application;
using ClassifiedAds.Contracts.AuditLog.DTOs;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.Subscription.Commands;
using ClassifiedAds.Modules.Subscription.Controllers;
using ClassifiedAds.Modules.Subscription.Models;
using ClassifiedAds.Modules.Subscription.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.Subscription;

public class PlansControllerTests
{
    private readonly Mock<ILogger<PlansController>> _loggerMock;
    private readonly Mock<ICommandHandler<AddUpdatePlanCommand>> _addUpdateHandlerMock;
    private readonly Mock<ICommandHandler<DeletePlanCommand>> _deleteHandlerMock;
    private readonly Mock<IQueryHandler<GetPlansQuery, List<PlanModel>>> _getPlansHandlerMock;
    private readonly Mock<IQueryHandler<GetPlanQuery, PlanModel>> _getPlanHandlerMock;
    private readonly Mock<IQueryHandler<GetAuditEntriesQuery, List<AuditLogEntryDTO>>> _getAuditEntriesHandlerMock;
    private readonly PlansController _controller;

    public PlansControllerTests()
    {
        _loggerMock = new Mock<ILogger<PlansController>>();
        _addUpdateHandlerMock = new Mock<ICommandHandler<AddUpdatePlanCommand>>();
        _deleteHandlerMock = new Mock<ICommandHandler<DeletePlanCommand>>();
        _getPlansHandlerMock = new Mock<IQueryHandler<GetPlansQuery, List<PlanModel>>>();
        _getPlanHandlerMock = new Mock<IQueryHandler<GetPlanQuery, PlanModel>>();
        _getAuditEntriesHandlerMock = new Mock<IQueryHandler<GetAuditEntriesQuery, List<AuditLogEntryDTO>>>();

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
        serviceProviderMock
            .Setup(x => x.GetService(typeof(IQueryHandler<GetAuditEntriesQuery, List<AuditLogEntryDTO>>)))
            .Returns(_getAuditEntriesHandlerMock.Object);

        _controller = new PlansController(new Dispatcher(serviceProviderMock.Object), _loggerMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };
    }

    [Fact]
    public async Task Get_Should_ReturnOkWithPlanList()
    {
        var plans = new List<PlanModel>
        {
            CreatePlanModel(Guid.NewGuid(), "free", "Free", true),
            CreatePlanModel(Guid.NewGuid(), "pro", "Professional", true),
        };

        _getPlansHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlansQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(plans);

        var result = await _controller.Get(isActive: true, search: "pro");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<PlanModel>>().Subject.Should().HaveCount(2);
    }

    [Fact]
    public async Task Get_Should_MapIsActiveAndSearchFilters()
    {
        GetPlansQuery capturedQuery = null!;

        _getPlansHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlansQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetPlansQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<PlanModel>());

        await _controller.Get(isActive: false, search: "enterprise");

        capturedQuery.Should().NotBeNull();
        capturedQuery.IsActive.Should().BeFalse();
        capturedQuery.Search.Should().Be("enterprise");
    }

    [Fact]
    public async Task GetById_Should_ReturnOkWithPlan()
    {
        var planId = Guid.NewGuid();
        var expected = CreatePlanModel(planId, "pro", "Professional", true);

        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.Get(planId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetById_Should_MapIdAndThrowNotFound()
    {
        var planId = Guid.NewGuid();
        GetPlanQuery capturedQuery = null!;

        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetPlanQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreatePlanModel(planId, "pro", "Professional", true));

        await _controller.Get(planId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.Id.Should().Be(planId);
        capturedQuery.ThrowNotFoundIfNull.Should().BeTrue();
    }

    [Fact]
    public async Task GetById_Should_PropagateNotFoundException()
    {
        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("PLAN_NOT_FOUND"));

        var act = () => _controller.Get(Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*PLAN_NOT_FOUND*");
    }

    [Fact]
    public async Task Post_Should_ReturnCreatedWithPlanPayload()
    {
        var savedPlanId = Guid.NewGuid();
        var expected = CreatePlanModel(savedPlanId, "pro", "Professional", true);

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdatePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdatePlanCommand, CancellationToken>((command, _) => command.SavedPlanId = savedPlanId)
            .Returns(Task.CompletedTask);
        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.Post(CreateUpdatePlanModel());

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/plans/{savedPlanId}");
        createdResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Post_Should_MapBodyIntoCommand()
    {
        AddUpdatePlanCommand capturedCommand = null!;
        var model = CreateUpdatePlanModel();

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdatePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdatePlanCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedPlanId = Guid.NewGuid();
            })
            .Returns(Task.CompletedTask);
        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePlanModel(Guid.NewGuid(), "pro", "Professional", true));

        await _controller.Post(model);

        capturedCommand.Should().NotBeNull();
        capturedCommand.PlanId.Should().BeNull();
        capturedCommand.Model.Should().BeSameAs(model);
    }

    [Fact]
    public async Task Post_Should_PropagateValidationException()
    {
        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdatePlanCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Thông tin gói cước là bắt buộc."));

        var act = () => _controller.Post(null!);

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*gói cước*");
    }

    [Fact]
    public async Task Put_Should_ReturnOkWithUpdatedPlanPayload()
    {
        var planId = Guid.NewGuid();
        var expected = CreatePlanModel(planId, "pro", "Professional", true);

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdatePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdatePlanCommand, CancellationToken>((command, _) => command.SavedPlanId = planId)
            .Returns(Task.CompletedTask);
        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.Put(planId, CreateUpdatePlanModel());

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Put_Should_MapIdAndBodyIntoCommand()
    {
        var planId = Guid.NewGuid();
        var model = CreateUpdatePlanModel();
        AddUpdatePlanCommand capturedCommand = null!;

        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdatePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddUpdatePlanCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.SavedPlanId = planId;
            })
            .Returns(Task.CompletedTask);
        _getPlanHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetPlanQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreatePlanModel(planId, "pro", "Professional", true));

        await _controller.Put(planId, model);

        capturedCommand.Should().NotBeNull();
        capturedCommand.PlanId.Should().Be(planId);
        capturedCommand.Model.Should().BeSameAs(model);
    }

    [Fact]
    public async Task Put_Should_PropagateNotFoundException()
    {
        _addUpdateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddUpdatePlanCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("PLAN_NOT_FOUND"));

        var act = () => _controller.Put(Guid.NewGuid(), CreateUpdatePlanModel());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*PLAN_NOT_FOUND*");
    }

    [Fact]
    public async Task Delete_Should_ReturnOk()
    {
        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeletePlanCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(Guid.NewGuid());

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task Delete_Should_MapPlanIdIntoCommand()
    {
        var planId = Guid.NewGuid();
        DeletePlanCommand capturedCommand = null!;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeletePlanCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeletePlanCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Delete(planId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.PlanId.Should().Be(planId);
    }

    [Fact]
    public async Task Delete_Should_PropagateValidationException()
    {
        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeletePlanCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Không thể ngừng kích hoạt gói vì vẫn còn thuê bao đang hoạt động."));

        var act = () => _controller.Delete(Guid.NewGuid());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*thuê bao*");
    }

    [Fact]
    public async Task GetAuditLogs_Should_ReturnOkWithEntries()
    {
        var planId = Guid.NewGuid();
        var older = CreateAuditLog(planId, "UPDATE_PLAN", DateTimeOffset.UtcNow.AddMinutes(-10), CreatePlanModel(planId, "pro", "Professional", true));
        var newer = CreateAuditLog(planId, "DELETE_PLAN", DateTimeOffset.UtcNow.AddMinutes(-5), CreatePlanModel(planId, "pro", "Professional+", false));

        _getAuditEntriesHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetAuditEntriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AuditLogEntryDTO> { older, newer });

        var result = await _controller.GetAuditLogs(planId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var entries = ((System.Collections.IEnumerable)okResult.Value!).Cast<object>().ToList();
        entries.Should().HaveCount(2);

        var firstAction = entries[0].GetType().GetProperty("Action")!.GetValue(entries[0])!.ToString();
        firstAction.Should().Be("DELETE");
    }

    [Fact]
    public async Task GetAuditLogs_Should_MapObjectId()
    {
        var planId = Guid.NewGuid();
        GetAuditEntriesQuery capturedQuery = null!;

        _getAuditEntriesHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetAuditEntriesQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetAuditEntriesQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<AuditLogEntryDTO>());

        await _controller.GetAuditLogs(planId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.ObjectId.Should().Be(planId.ToString());
    }

    private static PlanModel CreatePlanModel(Guid id, string name, string displayName, bool isActive)
    {
        return new PlanModel
        {
            Id = id,
            Name = name,
            DisplayName = displayName,
            Description = $"{displayName} plan",
            PriceMonthly = 100,
            PriceYearly = 1000,
            Currency = "VND",
            IsActive = isActive,
            SortOrder = 1,
            CreatedDateTime = DateTimeOffset.UtcNow.AddDays(-7),
        };
    }

    private static CreateUpdatePlanModel CreateUpdatePlanModel()
    {
        return new CreateUpdatePlanModel
        {
            Name = "pro",
            DisplayName = "Professional",
            Description = "Professional plan",
            PriceMonthly = 390000,
            PriceYearly = 3900000,
            Currency = "VND",
            IsActive = true,
            SortOrder = 1,
        };
    }

    private static AuditLogEntryDTO CreateAuditLog(Guid planId, string action, DateTimeOffset createdAt, PlanModel model)
    {
        return new AuditLogEntryDTO
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            UserName = "admin",
            Action = action,
            ObjectId = planId.ToString(),
            Log = JsonSerializer.Serialize(model),
            CreatedDateTime = createdAt,
        };
    }
}
