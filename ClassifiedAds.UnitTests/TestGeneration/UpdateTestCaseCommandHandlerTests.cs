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
/// Unit tests for UpdateTestCaseCommandHandler.
/// </summary>
public class UpdateTestCaseCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<TestCaseRequest, Guid>> _requestRepoMock;
    private readonly Mock<IRepository<TestCaseExpectation, Guid>> _expectationRepoMock;
    private readonly Mock<IRepository<TestCaseVariable, Guid>> _variableRepoMock;
    private readonly Mock<IRepository<TestCaseChangeLog, Guid>> _changeLogRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly UpdateTestCaseCommandHandler _handler;

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultSuiteId = Guid.NewGuid();
    private static readonly Guid DefaultTestCaseId = Guid.NewGuid();

    public UpdateTestCaseCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _requestRepoMock = new Mock<IRepository<TestCaseRequest, Guid>>();
        _expectationRepoMock = new Mock<IRepository<TestCaseExpectation, Guid>>();
        _variableRepoMock = new Mock<IRepository<TestCaseVariable, Guid>>();
        _changeLogRepoMock = new Mock<IRepository<TestCaseChangeLog, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _handler = new UpdateTestCaseCommandHandler(
            _suiteRepoMock.Object,
            _testCaseRepoMock.Object,
            _requestRepoMock.Object,
            _expectationRepoMock.Object,
            _variableRepoMock.Object,
            _changeLogRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenTestCaseNotFound()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupTestCaseNotFound();

        var command = CreateValidCommand(suite.CreatedById);

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var command = CreateValidCommand();
        command.CurrentUserId = Guid.NewGuid();

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*quyền*");
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
    public async Task HandleAsync_Should_UpdateAllFields()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        var testCase = CreateTestCase();
        SetupTestCaseFound(testCase);
        SetupRequestFound();
        SetupExpectationFound();
        SetupNoExistingVariables();

        var command = CreateValidCommand(suite.CreatedById);
        command.Name = "Updated Name";
        command.Description = "Updated description";

        await _handler.HandleAsync(command);

        _testCaseRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestCase>(tc => tc.Name == "Updated Name"),
            It.IsAny<CancellationToken>()), Times.Once);

        command.Result.Should().NotBeNull();
        command.Result.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task HandleAsync_Should_IncrementVersion()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        var testCase = CreateTestCase();
        testCase.Version = 2;
        SetupTestCaseFound(testCase);
        SetupRequestFound();
        SetupExpectationFound();
        SetupNoExistingVariables();

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _testCaseRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestCase>(tc => tc.Version == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ReplaceVariables()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        var testCase = CreateTestCase();
        SetupTestCaseFound(testCase);
        SetupRequestFound();
        SetupExpectationFound();

        // Setup 2 existing variables
        var existingVars = new List<TestCaseVariable>
        {
            new() { Id = Guid.NewGuid(), TestCaseId = DefaultTestCaseId, VariableName = "old1" },
            new() { Id = Guid.NewGuid(), TestCaseId = DefaultTestCaseId, VariableName = "old2" },
        };
        _variableRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseVariable>>()))
            .ReturnsAsync(existingVars);
        _variableRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(existingVars.AsQueryable());

        var command = CreateValidCommand(suite.CreatedById);
        command.Variables = new List<VariableInput>
        {
            new() { VariableName = "new1", ExtractFrom = ExtractFrom.ResponseBody, JsonPath = "$.id" },
        };

        await _handler.HandleAsync(command);

        // Verify old variables deleted
        _variableRepoMock.Verify(x => x.Delete(It.IsAny<TestCaseVariable>()), Times.Exactly(2));
        // Verify new variable added
        _variableRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseVariable>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_CreateChangeLog()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        var testCase = CreateTestCase();
        SetupTestCaseFound(testCase);
        SetupRequestFound();
        SetupExpectationFound();
        SetupNoExistingVariables();

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _changeLogRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseChangeLog>(cl =>
                cl.ChangeType == TestCaseChangeType.RequestChanged &&
                cl.ChangedById == command.CurrentUserId),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    #region Helpers

    private static TestSuite CreateSuite()
    {
        return new TestSuite
        {
            Id = DefaultSuiteId,
            CreatedById = DefaultUserId,
            Name = "Test Suite",
            Status = TestSuiteStatus.Ready,
            Version = 1,
            EndpointBusinessContexts = new Dictionary<Guid, string>(),
        };
    }

    private static TestCase CreateTestCase()
    {
        return new TestCase
        {
            Id = DefaultTestCaseId,
            TestSuiteId = DefaultSuiteId,
            Name = "Original Test",
            TestType = TestType.HappyPath,
            Priority = TestPriority.Medium,
            Version = 1,
        };
    }

    private static UpdateTestCaseCommand CreateValidCommand(Guid? userId = null)
    {
        return new UpdateTestCaseCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCaseId = DefaultTestCaseId,
            CurrentUserId = userId ?? DefaultUserId,
            Name = "Updated Test",
            TestType = TestType.Boundary,
            Priority = TestPriority.High,
            RequestHttpMethod = HttpMethodEnum.POST,
            RequestUrl = "/api/test",
            ExpectedStatus = "[200]",
        };
    }

    private void SetupSuiteFound(TestSuite suite)
    {
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
    }

    private void SetupTestCaseFound(TestCase testCase)
    {
        _testCaseRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(testCase);
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCase> { testCase }.AsQueryable());
    }

    private void SetupTestCaseNotFound()
    {
        _testCaseRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync((TestCase)null);
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCase>().AsQueryable());
    }

    private void SetupRequestFound()
    {
        _requestRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestCaseRequest>>()))
            .ReturnsAsync(new TestCaseRequest { Id = Guid.NewGuid(), TestCaseId = DefaultTestCaseId });
        _requestRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCaseRequest> { new() { Id = Guid.NewGuid(), TestCaseId = DefaultTestCaseId } }.AsQueryable());
    }

    private void SetupExpectationFound()
    {
        _expectationRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestCaseExpectation>>()))
            .ReturnsAsync(new TestCaseExpectation { Id = Guid.NewGuid(), TestCaseId = DefaultTestCaseId });
        _expectationRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCaseExpectation> { new() { Id = Guid.NewGuid(), TestCaseId = DefaultTestCaseId } }.AsQueryable());
    }

    private void SetupNoExistingVariables()
    {
        _variableRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseVariable>>()))
            .ReturnsAsync(new List<TestCaseVariable>());
        _variableRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCaseVariable>().AsQueryable());
    }

    #endregion
}
