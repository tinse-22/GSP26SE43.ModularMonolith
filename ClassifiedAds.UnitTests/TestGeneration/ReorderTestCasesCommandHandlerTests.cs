using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for ReorderTestCasesCommandHandler.
/// </summary>
public class ReorderTestCasesCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<TestCaseChangeLog, Guid>> _changeLogRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ReorderTestCasesCommandHandler _handler;

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultSuiteId = Guid.NewGuid();

    public ReorderTestCasesCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _changeLogRepoMock = new Mock<IRepository<TestCaseChangeLog, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _handler = new ReorderTestCasesCommandHandler(
            _suiteRepoMock.Object,
            _testCaseRepoMock.Object,
            _changeLogRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenTestCaseIdsDontBelongToSuite()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCases = CreateTestCases(2);
        SetupTestCasesFound(testCases);

        var command = new ReorderTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = suite.CreatedById,
            TestCaseIds = new List<Guid> { testCases[0].Id, Guid.NewGuid() }, // invalid ID
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*không thuộc*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenTestCaseIdsEmpty()
    {
        var command = new ReorderTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = DefaultUserId,
            TestCaseIds = new List<Guid>(),
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_SetCustomOrderIndexAndIsOrderCustomized()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCases = CreateTestCases(3);
        SetupTestCasesFound(testCases);

        // Reorder: reverse the order
        var command = new ReorderTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = suite.CreatedById,
            TestCaseIds = new List<Guid> { testCases[2].Id, testCases[0].Id, testCases[1].Id },
        };

        await _handler.HandleAsync(command);

        // Verify each test case was updated with CustomOrderIndex
        _testCaseRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestCase>(tc => tc.Id == testCases[2].Id && tc.CustomOrderIndex == 0 && tc.IsOrderCustomized),
            It.IsAny<CancellationToken>()), Times.Once);
        _testCaseRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestCase>(tc => tc.Id == testCases[0].Id && tc.CustomOrderIndex == 1 && tc.IsOrderCustomized),
            It.IsAny<CancellationToken>()), Times.Once);
        _testCaseRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestCase>(tc => tc.Id == testCases[1].Id && tc.CustomOrderIndex == 2 && tc.IsOrderCustomized),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_CreateChangeLogForEachCase()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCases = CreateTestCases(3);
        SetupTestCasesFound(testCases);

        var command = new ReorderTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = suite.CreatedById,
            TestCaseIds = testCases.Select(tc => tc.Id).ToList(),
        };

        await _handler.HandleAsync(command);

        _changeLogRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseChangeLog>(cl =>
                cl.ChangeType == TestCaseChangeType.UserCustomizedOrder &&
                cl.ChangedById == command.CurrentUserId),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNotAllTestCasesIncluded()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCases = CreateTestCases(3);
        SetupTestCasesFound(testCases);

        // Only send 2 out of 3 — missing one test case
        var command = new ReorderTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = suite.CreatedById,
            TestCaseIds = new List<Guid> { testCases[0].Id, testCases[1].Id },
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*thiếu*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenDuplicateIdsSubmitted()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCases = CreateTestCases(3);
        SetupTestCasesFound(testCases);

        // Send all 3 IDs but with a duplicate (4 entries total) — completeness passes, duplicate check fires
        var command = new ReorderTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = suite.CreatedById,
            TestCaseIds = new List<Guid> { testCases[0].Id, testCases[1].Id, testCases[2].Id, testCases[0].Id },
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*trùng*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var command = new ReorderTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            CurrentUserId = Guid.NewGuid(),
            TestCaseIds = new List<Guid> { Guid.NewGuid() },
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*quyền*");
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

    private static List<TestCase> CreateTestCases(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new TestCase
            {
                Id = Guid.NewGuid(),
                TestSuiteId = DefaultSuiteId,
                Name = $"Test Case {i}",
                OrderIndex = i,
                Version = 1,
            })
            .ToList();
    }

    private void SetupSuiteFound(TestSuite suite)
    {
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);
    }

    private void SetupTestCasesFound(List<TestCase> testCases)
    {
        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(testCases);
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(testCases.AsQueryable());
    }

    #endregion
}
