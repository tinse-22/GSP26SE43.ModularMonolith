using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for DeleteTestCaseCommandHandler.
/// </summary>
public class DeleteTestCaseCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<TestCaseChangeLog, Guid>> _changeLogRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly DeleteTestCaseCommandHandler _handler;

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultSuiteId = Guid.NewGuid();
    private static readonly Guid DefaultTestCaseId = Guid.NewGuid();

    public DeleteTestCaseCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _changeLogRepoMock = new Mock<IRepository<TestCaseChangeLog, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);
        _testCaseRepoMock.Setup(x => x.UpdateAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _suiteRepoMock.Setup(x => x.UpdateAsync(It.IsAny<TestSuite>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _changeLogRepoMock.Setup(x => x.AddAsync(It.IsAny<TestCaseChangeLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _unitOfWorkMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _handler = new DeleteTestCaseCommandHandler(
            _suiteRepoMock.Object,
            _testCaseRepoMock.Object,
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
    public async Task HandleAsync_Should_SoftDeleteTestCase()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCase = CreateTestCase();
        SetupTestCaseFound(testCase);
        SetupRemainingTestCases(0);

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _testCaseRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestCase>(tc => tc.Id == DefaultTestCaseId && tc.IsDeleted && !tc.IsEnabled),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_RecalculateOrderIndex()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCase = CreateTestCase();
        testCase.OrderIndex = 1;
        SetupTestCaseFound(testCase);

        var remaining = new List<TestCase>
        {
            new() { Id = Guid.NewGuid(), TestSuiteId = DefaultSuiteId, OrderIndex = 0 },
            new() { Id = Guid.NewGuid(), TestSuiteId = DefaultSuiteId, OrderIndex = 2 },
        };
        SetupRemainingTestCasesWithList(remaining);

        var reindexedCaseId = remaining[1].Id;

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        // The case that was at OrderIndex 2 should be recalculated to 1
        _testCaseRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestCase>(tc => tc.Id == reindexedCaseId && tc.OrderIndex == 1),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_CreateChangeLog()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCase = CreateTestCase();
        SetupTestCaseFound(testCase);
        SetupRemainingTestCases(0);

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _changeLogRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseChangeLog>(cl =>
                cl.ChangeType == TestCaseChangeType.Deleted &&
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
            Name = "Test to Delete",
            TestType = TestType.HappyPath,
            OrderIndex = 0,
            Version = 1,
        };
    }

    private static DeleteTestCaseCommand CreateValidCommand(Guid? userId = null)
    {
        return new DeleteTestCaseCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCaseId = DefaultTestCaseId,
            CurrentUserId = userId ?? DefaultUserId,
        };
    }

    private void SetupSuiteFound(TestSuite suite)
    {
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
        _suiteRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestSuite> { suite }.AsQueryable());
    }

    private void SetupTestCaseNotFound()
    {
        _testCaseRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync((TestCase)null);
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCase>().AsQueryable());
    }

    private void SetupTestCaseFound(TestCase testCase)
    {
        _testCaseRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(testCase);
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCase> { testCase }.AsQueryable());
    }

    private void SetupRemainingTestCases(int count)
    {
        var remaining = Enumerable.Range(0, count)
            .Select(i => new TestCase { Id = Guid.NewGuid(), TestSuiteId = DefaultSuiteId, OrderIndex = i })
            .ToList();
        SetupRemainingTestCasesWithList(remaining);
    }

    private void SetupRemainingTestCasesWithList(List<TestCase> remaining)
    {
        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(remaining);
    }

    #endregion
}
