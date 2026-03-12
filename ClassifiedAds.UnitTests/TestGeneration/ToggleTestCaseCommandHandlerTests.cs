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
/// Unit tests for ToggleTestCaseCommandHandler.
/// </summary>
public class ToggleTestCaseCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<TestCaseChangeLog, Guid>> _changeLogRepoMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly ToggleTestCaseCommandHandler _handler;

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultSuiteId = Guid.NewGuid();
    private static readonly Guid DefaultTestCaseId = Guid.NewGuid();

    public ToggleTestCaseCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _changeLogRepoMock = new Mock<IRepository<TestCaseChangeLog, Guid>>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _handler = new ToggleTestCaseCommandHandler(
            _suiteRepoMock.Object,
            _testCaseRepoMock.Object,
            _changeLogRepoMock.Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ToggleFromTrueToFalse()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCase = CreateTestCase(isEnabled: true);
        SetupTestCaseFound(testCase);

        var command = new ToggleTestCaseCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCaseId = DefaultTestCaseId,
            CurrentUserId = suite.CreatedById,
            IsEnabled = false,
        };

        await _handler.HandleAsync(command);

        _testCaseRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestCase>(tc => tc.IsEnabled == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ToggleFromFalseToTrue()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCase = CreateTestCase(isEnabled: false);
        SetupTestCaseFound(testCase);

        var command = new ToggleTestCaseCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCaseId = DefaultTestCaseId,
            CurrentUserId = suite.CreatedById,
            IsEnabled = true,
        };

        await _handler.HandleAsync(command);

        _testCaseRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestCase>(tc => tc.IsEnabled == true),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_CreateChangeLog()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var testCase = CreateTestCase(isEnabled: true);
        SetupTestCaseFound(testCase);

        var command = new ToggleTestCaseCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCaseId = DefaultTestCaseId,
            CurrentUserId = suite.CreatedById,
            IsEnabled = false,
        };

        await _handler.HandleAsync(command);

        _changeLogRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseChangeLog>(cl =>
                cl.ChangeType == TestCaseChangeType.EnabledStatusChanged &&
                cl.FieldName == "IsEnabled" &&
                cl.OldValue == "True" &&
                cl.NewValue == "False"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowNotFound_WhenTestCaseNotFound()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupTestCaseNotFound();

        var command = new ToggleTestCaseCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCaseId = DefaultTestCaseId,
            CurrentUserId = suite.CreatedById,
            IsEnabled = false,
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNotSuiteOwner()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);

        var command = new ToggleTestCaseCommand
        {
            TestSuiteId = DefaultSuiteId,
            TestCaseId = DefaultTestCaseId,
            CurrentUserId = Guid.NewGuid(),
            IsEnabled = false,
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

    private static TestCase CreateTestCase(bool isEnabled = true)
    {
        return new TestCase
        {
            Id = DefaultTestCaseId,
            TestSuiteId = DefaultSuiteId,
            Name = "Toggle Test",
            IsEnabled = isEnabled,
            Version = 1,
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

    #endregion
}
