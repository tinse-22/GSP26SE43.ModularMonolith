using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Controllers;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class TestCasesControllerTests
{
    private readonly Mock<ICurrentUser> _currentUserMock;
    private readonly Mock<ILogger<TestCasesController>> _loggerMock;
    private readonly Mock<ICommandHandler<AddTestCaseCommand>> _addHandlerMock;
    private readonly Mock<ICommandHandler<UpdateTestCaseCommand>> _updateHandlerMock;
    private readonly Mock<ICommandHandler<DeleteTestCaseCommand>> _deleteHandlerMock;
    private readonly Mock<ICommandHandler<ReorderTestCasesCommand>> _reorderHandlerMock;
    private readonly Mock<IQueryHandler<GetTestCasesByTestSuiteQuery, List<TestCaseModel>>> _getAllHandlerMock;
    private readonly Mock<IQueryHandler<GetTestCaseDetailQuery, TestCaseModel>> _getByIdHandlerMock;
    private readonly TestCasesController _controller;
    private readonly Guid _currentUserId = Guid.NewGuid();

    public TestCasesControllerTests()
    {
        _currentUserMock = new Mock<ICurrentUser>();
        _loggerMock = new Mock<ILogger<TestCasesController>>();
        _addHandlerMock = new Mock<ICommandHandler<AddTestCaseCommand>>();
        _updateHandlerMock = new Mock<ICommandHandler<UpdateTestCaseCommand>>();
        _deleteHandlerMock = new Mock<ICommandHandler<DeleteTestCaseCommand>>();
        _reorderHandlerMock = new Mock<ICommandHandler<ReorderTestCasesCommand>>();
        _getAllHandlerMock = new Mock<IQueryHandler<GetTestCasesByTestSuiteQuery, List<TestCaseModel>>>();
        _getByIdHandlerMock = new Mock<IQueryHandler<GetTestCaseDetailQuery, TestCaseModel>>();

        _currentUserMock.SetupGet(x => x.UserId).Returns(_currentUserId);
        _currentUserMock.SetupGet(x => x.IsAuthenticated).Returns(true);

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<AddTestCaseCommand>))).Returns(_addHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<UpdateTestCaseCommand>))).Returns(_updateHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<DeleteTestCaseCommand>))).Returns(_deleteHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(ICommandHandler<ReorderTestCasesCommand>))).Returns(_reorderHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetTestCasesByTestSuiteQuery, List<TestCaseModel>>))).Returns(_getAllHandlerMock.Object);
        serviceProviderMock.Setup(x => x.GetService(typeof(IQueryHandler<GetTestCaseDetailQuery, TestCaseModel>))).Returns(_getByIdHandlerMock.Object);

        var dispatcher = new Dispatcher(serviceProviderMock.Object);
        _controller = new TestCasesController(
            dispatcher,
            _currentUserMock.Object,
            _loggerMock.Object,
            Options.Create(new N8nIntegrationOptions()));
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GetAll_Should_ReturnOkWithTestCases()
    {
        var suiteId = Guid.NewGuid();
        var cases = new List<TestCaseModel>
        {
            CreateModel(suiteId, name: "Happy path login", testType: "HappyPath"),
            CreateModel(suiteId, name: "Boundary token refresh", testType: "Boundary"),
        };

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestCasesByTestSuiteQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cases);

        var result = await _controller.GetAll(suiteId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = okResult.Value.Should().BeAssignableTo<List<TestCaseModel>>().Subject;
        payload.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAll_Should_MapFiltersAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        GetTestCasesByTestSuiteQuery capturedQuery = null!;

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestCasesByTestSuiteQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestCasesByTestSuiteQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<TestCaseModel>());

        await _controller.GetAll(suiteId, "Boundary", includeDisabled: true, includeDeleted: true);

        capturedQuery.Should().NotBeNull();
        capturedQuery.TestSuiteId.Should().Be(suiteId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
        capturedQuery.FilterByTestType.Should().Be(TestType.Boundary);
        capturedQuery.IncludeDisabled.Should().BeTrue();
        capturedQuery.IncludeDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetAll_Should_DefaultToNullFilter_WhenTestTypeIsInvalid()
    {
        GetTestCasesByTestSuiteQuery capturedQuery = null!;

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestCasesByTestSuiteQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestCasesByTestSuiteQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(new List<TestCaseModel>());

        await _controller.GetAll(Guid.NewGuid(), "NotARealType", includeDisabled: false, includeDeleted: false);

        capturedQuery.Should().NotBeNull();
        capturedQuery.FilterByTestType.Should().BeNull();
        capturedQuery.IncludeDisabled.Should().BeFalse();
        capturedQuery.IncludeDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task GetAll_Should_ReturnOkWithEmptyList()
    {
        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestCasesByTestSuiteQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestCaseModel>());

        var result = await _controller.GetAll(Guid.NewGuid());

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeAssignableTo<List<TestCaseModel>>().Subject.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_Should_ReturnMetadataFlagsForDisabledAndDeletedCases()
    {
        var suiteId = Guid.NewGuid();
        var deletedAt = DateTimeOffset.UtcNow;
        var testCase = CreateModel(suiteId, name: "Soft deleted case", testType: "Negative");
        testCase.IsEnabled = false;
        testCase.IsDeleted = true;
        testCase.DeletedAt = deletedAt;

        _getAllHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestCasesByTestSuiteQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TestCaseModel> { testCase });

        var result = await _controller.GetAll(suiteId, includeDisabled: true, includeDeleted: true);

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeAssignableTo<List<TestCaseModel>>().Subject;
        payload[0].IsEnabled.Should().BeFalse();
        payload[0].IsDeleted.Should().BeTrue();
        payload[0].DeletedAt.Should().Be(deletedAt);
    }

    [Fact]
    public async Task GetById_Should_ReturnOkWithTestCaseDetail()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestCaseDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateModel(suiteId, testCaseId, "Detail test case"));

        var result = await _controller.GetById(suiteId, testCaseId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<TestCaseModel>().Subject.Id.Should().Be(testCaseId);
    }

    [Fact]
    public async Task GetById_Should_MapRouteIdentifiersAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        GetTestCaseDetailQuery capturedQuery = null!;

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestCaseDetailQuery>(), It.IsAny<CancellationToken>()))
            .Callback<GetTestCaseDetailQuery, CancellationToken>((query, _) => capturedQuery = query)
            .ReturnsAsync(CreateModel(suiteId, testCaseId, "Detail test case"));

        await _controller.GetById(suiteId, testCaseId);

        capturedQuery.Should().NotBeNull();
        capturedQuery.TestSuiteId.Should().Be(suiteId);
        capturedQuery.TestCaseId.Should().Be(testCaseId);
        capturedQuery.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task GetById_Should_ReturnNestedRequestExpectationAndVariables()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();

        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestCaseDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateModel(suiteId, testCaseId, "Detail test case"));

        var result = await _controller.GetById(suiteId, testCaseId);

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<TestCaseModel>().Subject;
        payload.Request.Should().NotBeNull();
        payload.Request!.HttpMethod.Should().Be("POST");
        payload.Expectation.Should().NotBeNull();
        payload.Expectation!.ExpectedStatus.Should().Be("201");
        payload.Variables.Should().ContainSingle();
    }

    [Fact]
    public async Task GetById_Should_ThrowNotFoundException_WhenCaseMissing()
    {
        _getByIdHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<GetTestCaseDetailQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Test case not found"));

        var act = () => _controller.GetById(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Test case not found*");
    }

    [Fact]
    public async Task Add_Should_ReturnCreatedWithTestCaseModel()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var request = CreateAddRequest();

        _addHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddTestCaseCommand, CancellationToken>((command, _) =>
            {
                command.Result = CreateModel(suiteId, testCaseId, request.Name);
            })
            .Returns(Task.CompletedTask);

        var result = await _controller.Add(suiteId, request);

        var createdResult = result.Result.Should().BeOfType<CreatedResult>().Subject;
        createdResult.Location.Should().Be($"/api/test-suites/{suiteId}/test-cases/{testCaseId}");
        createdResult.Value.Should().BeOfType<TestCaseModel>().Subject.Id.Should().Be(testCaseId);
    }

    [Fact]
    public async Task Add_Should_MapTopLevelFieldsIntoCommand()
    {
        var suiteId = Guid.NewGuid();
        var request = CreateAddRequest();
        AddTestCaseCommand capturedCommand = null!;

        _addHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddTestCaseCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateModel(suiteId, Guid.NewGuid(), request.Name);
            })
            .Returns(Task.CompletedTask);

        await _controller.Add(suiteId, request);

        capturedCommand.Should().NotBeNull();
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.EndpointId.Should().Be(request.EndpointId);
        capturedCommand.Name.Should().Be(request.Name);
        capturedCommand.Description.Should().Be(request.Description);
        capturedCommand.TestType.Should().Be(TestType.HappyPath);
        capturedCommand.Priority.Should().Be(TestPriority.High);
        capturedCommand.IsEnabled.Should().BeTrue();
        capturedCommand.Tags.Should().BeEquivalentTo(request.Tags);
    }

    [Fact]
    public async Task Add_Should_MapRequestAndExpectationFieldsIntoCommand()
    {
        var suiteId = Guid.NewGuid();
        AddTestCaseCommand capturedCommand = null!;

        _addHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddTestCaseCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateModel(suiteId, Guid.NewGuid(), "Created case");
            })
            .Returns(Task.CompletedTask);

        await _controller.Add(suiteId, CreateAddRequest());

        capturedCommand.RequestHttpMethod.Should().Be(HttpMethod.POST);
        capturedCommand.RequestUrl.Should().Be("/auth/login");
        capturedCommand.RequestHeaders.Should().Contain("Content-Type");
        capturedCommand.RequestBodyType.Should().Be(BodyType.Json);
        capturedCommand.RequestTimeout.Should().Be(15000);
        capturedCommand.ExpectedStatus.Should().Be("201");
        capturedCommand.ResponseSchema.Should().Contain("token");
        capturedCommand.BodyContains.Should().Be("$.token");
        capturedCommand.MaxResponseTime.Should().Be(2000);
        capturedCommand.ExpectedProvenance.Should().Be("OpenAPI");
    }

    [Fact]
    public async Task Add_Should_MapVariablesIntoCommand()
    {
        var suiteId = Guid.NewGuid();
        AddTestCaseCommand capturedCommand = null!;

        _addHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddTestCaseCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateModel(suiteId, Guid.NewGuid(), "Created case");
            })
            .Returns(Task.CompletedTask);

        await _controller.Add(suiteId, CreateAddRequest());

        capturedCommand.Variables.Should().ContainSingle();
        capturedCommand.Variables[0].VariableName.Should().Be("accessToken");
        capturedCommand.Variables[0].ExtractFrom.Should().Be(ExtractFrom.ResponseBody);
        capturedCommand.Variables[0].JsonPath.Should().Be("$.token");
    }

    [Fact]
    public async Task Add_Should_DefaultVariablesToEmptyList_WhenRequestOmitsVariables()
    {
        var suiteId = Guid.NewGuid();
        var request = CreateAddRequest();
        request.Variables = null;
        AddTestCaseCommand capturedCommand = null!;

        _addHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddTestCaseCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateModel(suiteId, Guid.NewGuid(), request.Name);
            })
            .Returns(Task.CompletedTask);

        await _controller.Add(suiteId, request);

        capturedCommand.Variables.Should().NotBeNull();
        capturedCommand.Variables.Should().BeEmpty();
    }

    [Fact]
    public async Task Add_Should_ThrowValidationException_WhenCreateFails()
    {
        _addHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<AddTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Test case name already exists"));

        var act = () => _controller.Add(Guid.NewGuid(), CreateAddRequest());

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Test case name already exists*");
    }

    [Fact]
    public async Task Update_Should_ReturnOkWithUpdatedModel()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var request = CreateUpdateRequest();

        _updateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateTestCaseCommand, CancellationToken>((command, _) =>
            {
                command.Result = CreateModel(suiteId, testCaseId, request.Name, testType: "Negative");
            })
            .Returns(Task.CompletedTask);

        var result = await _controller.Update(suiteId, testCaseId, request);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<TestCaseModel>().Subject.Id.Should().Be(testCaseId);
    }

    [Fact]
    public async Task Update_Should_MapSuiteCaseAndCurrentUserIntoCommand()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        UpdateTestCaseCommand capturedCommand = null!;

        _updateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateTestCaseCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateModel(suiteId, testCaseId, "Updated case");
            })
            .Returns(Task.CompletedTask);

        await _controller.Update(suiteId, testCaseId, CreateUpdateRequest());

        capturedCommand.Should().NotBeNull();
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.TestCaseId.Should().Be(testCaseId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Update_Should_MapTopLevelFieldsIncludingEndpointAndTags()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var request = CreateUpdateRequest();
        UpdateTestCaseCommand capturedCommand = null!;

        _updateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateTestCaseCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateModel(suiteId, testCaseId, request.Name, testType: "Negative");
            })
            .Returns(Task.CompletedTask);

        await _controller.Update(suiteId, testCaseId, request);

        capturedCommand.EndpointId.Should().Be(request.EndpointId);
        capturedCommand.Name.Should().Be(request.Name);
        capturedCommand.Description.Should().Be(request.Description);
        capturedCommand.TestType.Should().Be(TestType.Negative);
        capturedCommand.Priority.Should().Be(TestPriority.Critical);
        capturedCommand.IsEnabled.Should().BeFalse();
        capturedCommand.Tags.Should().BeEquivalentTo(request.Tags);
    }

    [Fact]
    public async Task Update_Should_MapNestedRequestAndExpectationFields()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        UpdateTestCaseCommand capturedCommand = null!;

        _updateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateTestCaseCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateModel(suiteId, testCaseId, "Updated case", testType: "Negative");
            })
            .Returns(Task.CompletedTask);

        await _controller.Update(suiteId, testCaseId, CreateUpdateRequest());

        capturedCommand.RequestHttpMethod.Should().Be(HttpMethod.PUT);
        capturedCommand.RequestUrl.Should().Be("/users/42");
        capturedCommand.RequestBodyType.Should().Be(BodyType.Json);
        capturedCommand.RequestTimeout.Should().Be(20000);
        capturedCommand.ExpectedStatus.Should().Be("400");
        capturedCommand.HeaderChecks.Should().Contain("trace-id");
        capturedCommand.BodyNotContains.Should().Contain("password");
        capturedCommand.JsonPathChecks.Should().Contain("$.errors");
        capturedCommand.ExpectedProvenance.Should().Be("SRS");
    }

    [Fact]
    public async Task Update_Should_MapVariablesIntoCommand()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        UpdateTestCaseCommand capturedCommand = null!;

        _updateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateTestCaseCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateModel(suiteId, testCaseId, "Updated case", testType: "Negative");
            })
            .Returns(Task.CompletedTask);

        await _controller.Update(suiteId, testCaseId, CreateUpdateRequest());

        capturedCommand.Variables.Should().ContainSingle();
        capturedCommand.Variables[0].VariableName.Should().Be("errorCode");
        capturedCommand.Variables[0].ExtractFrom.Should().Be(ExtractFrom.ResponseBody);
    }

    [Fact]
    public async Task Update_Should_ReturnUpdatedPayloadFromCommand()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var expected = CreateModel(suiteId, testCaseId, "Updated case", testType: "Security");
        expected.OrderIndex = 9;

        _updateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateTestCaseCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.Update(suiteId, testCaseId, CreateUpdateRequest());

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<TestCaseModel>().Subject;
        payload.TestType.Should().Be("Security");
        payload.OrderIndex.Should().Be(9);
    }

    [Fact]
    public async Task Update_Should_ThrowNotFoundException_WhenCaseMissing()
    {
        _updateHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<UpdateTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Test case not found"));

        var act = () => _controller.Update(Guid.NewGuid(), Guid.NewGuid(), CreateUpdateRequest());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Test case not found*");
    }

    [Fact]
    public async Task Delete_Should_ReturnOkWithDeletedModel()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var expected = CreateModel(suiteId, testCaseId, "Deleted case");
        expected.IsDeleted = true;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteTestCaseCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(suiteId, testCaseId);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeOfType<TestCaseModel>().Subject.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_Should_MapIdentifiersAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        DeleteTestCaseCommand capturedCommand = null!;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteTestCaseCommand, CancellationToken>((command, _) =>
            {
                capturedCommand = command;
                command.Result = CreateModel(suiteId, testCaseId, "Deleted case");
            })
            .Returns(Task.CompletedTask);

        await _controller.Delete(suiteId, testCaseId);

        capturedCommand.Should().NotBeNull();
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.TestCaseId.Should().Be(testCaseId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
    }

    [Fact]
    public async Task Delete_Should_ReturnDeletedPayloadFromCommand()
    {
        var suiteId = Guid.NewGuid();
        var testCaseId = Guid.NewGuid();
        var expected = CreateModel(suiteId, testCaseId, "Deleted case");
        expected.IsDeleted = true;
        expected.DeletedAt = DateTimeOffset.UtcNow;

        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteTestCaseCommand, CancellationToken>((command, _) => command.Result = expected)
            .Returns(Task.CompletedTask);

        var result = await _controller.Delete(suiteId, testCaseId);

        var payload = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value
            .Should().BeOfType<TestCaseModel>().Subject;
        payload.IsDeleted.Should().BeTrue();
        payload.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Delete_Should_ThrowNotFoundException_WhenCaseMissing()
    {
        _deleteHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<DeleteTestCaseCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("Test case not found"));

        var act = () => _controller.Delete(Guid.NewGuid(), Guid.NewGuid());

        await act.Should().ThrowAsync<NotFoundException>()
            .WithMessage("*Test case not found*");
    }

    [Fact]
    public async Task Reorder_Should_ReturnOk()
    {
        _reorderHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ReorderTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await _controller.Reorder(Guid.NewGuid(), new ReorderTestCasesRequest
        {
            TestCaseIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() },
        });

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task Reorder_Should_MapSuiteIdsAndCurrentUser()
    {
        var suiteId = Guid.NewGuid();
        var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        ReorderTestCasesCommand capturedCommand = null!;

        _reorderHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ReorderTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ReorderTestCasesCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Reorder(suiteId, new ReorderTestCasesRequest { TestCaseIds = ids });

        capturedCommand.Should().NotBeNull();
        capturedCommand.TestSuiteId.Should().Be(suiteId);
        capturedCommand.CurrentUserId.Should().Be(_currentUserId);
        capturedCommand.TestCaseIds.Should().BeEquivalentTo(ids, options => options.WithStrictOrdering());
    }

    [Fact]
    public async Task Reorder_Should_ForwardExactOrderAndCount()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        ReorderTestCasesCommand capturedCommand = null!;

        _reorderHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ReorderTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ReorderTestCasesCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        await _controller.Reorder(Guid.NewGuid(), new ReorderTestCasesRequest
        {
            TestCaseIds = new List<Guid> { id3, id1, id2 },
        });

        capturedCommand.TestCaseIds.Should().Equal(id3, id1, id2);
        capturedCommand.TestCaseIds.Should().HaveCount(3);
    }

    [Fact]
    public async Task Reorder_Should_AllowEmptyRequestList()
    {
        ReorderTestCasesCommand capturedCommand = null!;

        _reorderHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ReorderTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .Callback<ReorderTestCasesCommand, CancellationToken>((command, _) => capturedCommand = command)
            .Returns(Task.CompletedTask);

        var result = await _controller.Reorder(Guid.NewGuid(), new ReorderTestCasesRequest
        {
            TestCaseIds = new List<Guid>(),
        });

        result.Should().BeOfType<OkResult>();
        capturedCommand.TestCaseIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Reorder_Should_ThrowValidationException_WhenSequenceInvalid()
    {
        _reorderHandlerMock
            .Setup(x => x.HandleAsync(It.IsAny<ReorderTestCasesCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ValidationException("Reorder request contains invalid test case ids"));

        var act = () => _controller.Reorder(Guid.NewGuid(), new ReorderTestCasesRequest
        {
            TestCaseIds = new List<Guid> { Guid.NewGuid() },
        });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*Reorder request contains invalid test case ids*");
    }

    private static AddTestCaseRequest CreateAddRequest()
    {
        return new AddTestCaseRequest
        {
            EndpointId = Guid.NewGuid(),
            Name = "Create login happy path case",
            Description = "Validate login success flow",
            TestType = TestType.HappyPath,
            Priority = TestPriority.High,
            IsEnabled = true,
            Tags = new List<string> { "auth", "login" },
            Request = new TestCaseRequestInput
            {
                HttpMethod = HttpMethod.POST,
                Url = "/auth/login",
                Headers = "{\"Content-Type\":\"application/json\"}",
                PathParams = "{}",
                QueryParams = "{}",
                BodyType = BodyType.Json,
                Body = "{\"email\":\"user@test.com\",\"password\":\"Pass@123\"}",
                Timeout = 15000,
            },
            Expectation = new TestCaseExpectationInput
            {
                ExpectedStatus = "201",
                ResponseSchema = "{\"token\":\"string\"}",
                HeaderChecks = "{\"Set-Cookie\":\"exists\"}",
                BodyContains = "$.token",
                BodyNotContains = "$.error",
                JsonPathChecks = "$.user.id",
                MaxResponseTime = 2000,
                ExpectedProvenance = "OpenAPI",
            },
            Variables = new List<TestCaseVariableInput>
            {
                new()
                {
                    VariableName = "accessToken",
                    ExtractFrom = ExtractFrom.ResponseBody,
                    JsonPath = "$.token",
                    DefaultValue = string.Empty,
                },
            },
        };
    }

    private static UpdateTestCaseRequest CreateUpdateRequest()
    {
        return new UpdateTestCaseRequest
        {
            EndpointId = Guid.NewGuid(),
            Name = "Reject invalid profile update",
            Description = "Validate validation error response",
            TestType = TestType.Negative,
            Priority = TestPriority.Critical,
            IsEnabled = false,
            Tags = new List<string> { "users", "validation" },
            Request = new TestCaseRequestInput
            {
                HttpMethod = HttpMethod.PUT,
                Url = "/users/42",
                Headers = "{\"Content-Type\":\"application/json\"}",
                PathParams = "{\"id\":\"42\"}",
                QueryParams = "{}",
                BodyType = BodyType.Json,
                Body = "{\"name\":\"\"}",
                Timeout = 20000,
            },
            Expectation = new TestCaseExpectationInput
            {
                ExpectedStatus = "400",
                ResponseSchema = "{\"errors\":[]}",
                HeaderChecks = "{\"trace-id\":\"exists\"}",
                BodyContains = "$.errors[0]",
                BodyNotContains = "$.password",
                JsonPathChecks = "$.errors",
                MaxResponseTime = 3000,
                ExpectedProvenance = "SRS",
            },
            Variables = new List<TestCaseVariableInput>
            {
                new()
                {
                    VariableName = "errorCode",
                    ExtractFrom = ExtractFrom.ResponseBody,
                    JsonPath = "$.errors[0].code",
                    DefaultValue = "VALIDATION_FAILED",
                },
            },
        };
    }

    private static TestCaseModel CreateModel(Guid suiteId, Guid? testCaseId = null, string name = "Test case", string testType = "HappyPath")
    {
        return new TestCaseModel
        {
            Id = testCaseId ?? Guid.NewGuid(),
            TestSuiteId = suiteId,
            EndpointId = Guid.NewGuid(),
            Name = name,
            Description = "Test case description",
            TestType = testType,
            Priority = "High",
            IsEnabled = true,
            OrderIndex = 2,
            Tags = new List<string> { "auth" },
            Version = 1,
            IsDeleted = false,
            CreatedDateTime = DateTimeOffset.UtcNow,
            RowVersion = "cm93VmVyc2lvbg==",
            Request = new TestCaseRequestModel
            {
                Id = Guid.NewGuid(),
                HttpMethod = "POST",
                Url = "/auth/login",
                Headers = "{\"Content-Type\":\"application/json\"}",
                PathParams = "{}",
                QueryParams = "{}",
                BodyType = "Json",
                Body = "{\"email\":\"user@test.com\"}",
                Timeout = 15000,
            },
            Expectation = new TestCaseExpectationModel
            {
                Id = Guid.NewGuid(),
                ExpectedStatus = "201",
                ResponseSchema = "{\"token\":\"string\"}",
                HeaderChecks = "{\"Set-Cookie\":\"exists\"}",
                BodyContains = "$.token",
                BodyNotContains = "$.error",
                JsonPathChecks = "$.user.id",
                MaxResponseTime = 2000,
                ExpectedProvenance = "OpenAPI",
            },
            Variables = new List<TestCaseVariableModel>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    VariableName = "accessToken",
                    ExtractFrom = "ResponseBody",
                    JsonPath = "$.token",
                    DefaultValue = string.Empty,
                },
            },
        };
    }
}
