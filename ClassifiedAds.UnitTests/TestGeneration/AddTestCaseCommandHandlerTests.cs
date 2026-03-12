using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using HttpMethodEnum = ClassifiedAds.Modules.TestGeneration.Entities.HttpMethod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for AddTestCaseCommandHandler.
/// </summary>
public class AddTestCaseCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<TestCaseRequest, Guid>> _requestRepoMock;
    private readonly Mock<IRepository<TestCaseExpectation, Guid>> _expectationRepoMock;
    private readonly Mock<IRepository<TestCaseVariable, Guid>> _variableRepoMock;
    private readonly Mock<IRepository<TestCaseChangeLog, Guid>> _changeLogRepoMock;
    private readonly Mock<IRepository<TestSuiteVersion, Guid>> _versionRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly AddTestCaseCommandHandler _handler;

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultSuiteId = Guid.NewGuid();

    public AddTestCaseCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _requestRepoMock = new Mock<IRepository<TestCaseRequest, Guid>>();
        _expectationRepoMock = new Mock<IRepository<TestCaseExpectation, Guid>>();
        _variableRepoMock = new Mock<IRepository<TestCaseVariable, Guid>>();
        _changeLogRepoMock = new Mock<IRepository<TestCaseChangeLog, Guid>>();
        _versionRepoMock = new Mock<IRepository<TestSuiteVersion, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _handler = new AddTestCaseCommandHandler(
            _suiteRepoMock.Object,
            _testCaseRepoMock.Object,
            _requestRepoMock.Object,
            _expectationRepoMock.Object,
            _variableRepoMock.Object,
            _changeLogRepoMock.Object,
            _versionRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenTestSuiteIdEmpty()
    {
        var command = CreateValidCommand();
        command.TestSuiteId = Guid.Empty;

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNameEmpty()
    {
        var command = CreateValidCommand();
        command.Name = "";

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNameExceeds200Chars()
    {
        var command = CreateValidCommand();
        command.Name = new string('A', 201);

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*200*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenSuiteDoesNotExist()
    {
        SetupSuiteNotFound();
        var command = CreateValidCommand();

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var command = CreateValidCommand();
        command.CurrentUserId = Guid.NewGuid(); // Different user

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*quyền*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSuiteIsArchived()
    {
        var suite = CreateSuite();
        suite.Status = TestSuiteStatus.Archived;
        SetupSuiteFound(suite);

        var command = CreateValidCommand(suite.CreatedById);

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*archived*");
    }

    [Fact]
    public async Task HandleAsync_Should_CreateTestCase_WithRequestExpectationVariables()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupNoExistingTestCases();

        var command = CreateValidCommand(suite.CreatedById);
        command.Variables = new List<VariableInput>
        {
            new() { VariableName = "token", ExtractFrom = ExtractFrom.ResponseBody, JsonPath = "$.token" },
        };

        await _handler.HandleAsync(command);

        _testCaseRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()), Times.Once);
        _requestRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        _expectationRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseExpectation>(), It.IsAny<CancellationToken>()), Times.Once);
        _variableRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseVariable>(), It.IsAny<CancellationToken>()), Times.Once);

        command.Result.Should().NotBeNull();
        command.Result.Name.Should().Be("Test Login");
    }

    [Fact]
    public async Task HandleAsync_Should_CreateChangeLog()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupNoExistingTestCases();

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _changeLogRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseChangeLog>(cl =>
                cl.ChangeType == TestCaseChangeType.Created &&
                cl.ChangedById == command.CurrentUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_IncrementSuiteVersion()
    {
        var suite = CreateSuite();
        suite.Version = 3;
        SetupSuiteFound(suite);
        SetupNoExistingTestCases();

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _suiteRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestSuite>(s => s.Version == 4),
            It.IsAny<CancellationToken>()), Times.Once);

        _versionRepoMock.Verify(x => x.AddAsync(
            It.Is<TestSuiteVersion>(v =>
                v.TestSuiteId == suite.Id &&
                v.ChangedById == command.CurrentUserId &&
                v.ChangeType == VersionChangeType.TestCasesModified),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_SetCorrectOrderIndex()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupExistingTestCases(3); // OrderIndex 0, 1, 2

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _testCaseRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCase>(tc => tc.OrderIndex == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Helpers

    private static TestSuite CreateSuite()
    {
        return new TestSuite
        {
            Id = DefaultSuiteId,
            CreatedById = DefaultUserId,
            Name = "Test Suite CRUD",
            Status = TestSuiteStatus.Ready,
            ApprovalStatus = ApprovalStatus.Approved,
            Version = 1,
            EndpointBusinessContexts = new Dictionary<Guid, string>(),
        };
    }

    private static AddTestCaseCommand CreateValidCommand(Guid? userId = null)
    {
        return new AddTestCaseCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = userId ?? DefaultUserId,
            Name = "Test Login",
            Description = "Test login endpoint",
            TestType = TestType.HappyPath,
            Priority = TestPriority.High,
            IsEnabled = true,
            RequestHttpMethod = HttpMethodEnum.POST,
            RequestUrl = "/api/auth/login",
            ExpectedStatus = "[200]",
        };
    }

    private void SetupSuiteFound(TestSuite suite)
    {
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
    }

    private void SetupSuiteNotFound()
    {
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync((TestSuite)null);
    }

    private void SetupNoExistingTestCases()
    {
        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(new List<TestCase>());
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCase>().AsQueryable());
    }

    private void SetupExistingTestCases(int count)
    {
        var existing = Enumerable.Range(0, count)
            .Select(i => new TestCase
            {
                Id = Guid.NewGuid(),
                TestSuiteId = DefaultSuiteId,
                OrderIndex = i,
            })
            .ToList();

        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(existing);
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(existing.AsQueryable());
    }

    #endregion
}
