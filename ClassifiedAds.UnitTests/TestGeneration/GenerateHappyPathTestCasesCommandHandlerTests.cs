using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using HttpMethodEnum = ClassifiedAds.Modules.TestGeneration.Entities.HttpMethod;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// FE-05B: GenerateHappyPathTestCasesCommandHandler unit tests.
/// Verifies gate check, subscription limits, force-regenerate, entity persistence,
/// and error handling for the happy-path test case generation pipeline.
/// </summary>
public class GenerateHappyPathTestCasesCommandHandlerTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<TestCaseDependency, Guid>> _dependencyRepoMock;
    private readonly Mock<IRepository<TestCaseRequest, Guid>> _requestRepoMock;
    private readonly Mock<IRepository<TestCaseExpectation, Guid>> _expectationRepoMock;
    private readonly Mock<IRepository<TestCaseVariable, Guid>> _variableRepoMock;
    private readonly Mock<IRepository<TestCaseChangeLog, Guid>> _changeLogRepoMock;
    private readonly Mock<IRepository<TestSuiteVersion, Guid>> _versionRepoMock;
    private readonly Mock<IApiTestOrderGateService> _gateServiceMock;
    private readonly Mock<IHappyPathTestCaseGenerator> _generatorMock;
    private readonly Mock<ISubscriptionLimitGatewayService> _subscriptionMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly GenerateHappyPathTestCasesCommandHandler _handler;

    public GenerateHappyPathTestCasesCommandHandlerTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _dependencyRepoMock = new Mock<IRepository<TestCaseDependency, Guid>>();
        _requestRepoMock = new Mock<IRepository<TestCaseRequest, Guid>>();
        _expectationRepoMock = new Mock<IRepository<TestCaseExpectation, Guid>>();
        _variableRepoMock = new Mock<IRepository<TestCaseVariable, Guid>>();
        _changeLogRepoMock = new Mock<IRepository<TestCaseChangeLog, Guid>>();
        _versionRepoMock = new Mock<IRepository<TestSuiteVersion, Guid>>();
        _gateServiceMock = new Mock<IApiTestOrderGateService>();
        _generatorMock = new Mock<IHappyPathTestCaseGenerator>();
        _subscriptionMock = new Mock<ISubscriptionLimitGatewayService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _handler = new GenerateHappyPathTestCasesCommandHandler(
            _suiteRepoMock.Object,
            _testCaseRepoMock.Object,
            _dependencyRepoMock.Object,
            _requestRepoMock.Object,
            _expectationRepoMock.Object,
            _variableRepoMock.Object,
            _changeLogRepoMock.Object,
            _versionRepoMock.Object,
            _gateServiceMock.Object,
            _generatorMock.Object,
            _subscriptionMock.Object,
            new Mock<ILogger<GenerateHappyPathTestCasesCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenTestSuiteIdEmpty()
    {
        var command = new GenerateHappyPathTestCasesCommand
        {
            TestSuiteId = Guid.Empty,
            SpecificationId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSpecificationIdEmpty()
    {
        var command = new GenerateHappyPathTestCasesCommand
        {
            TestSuiteId = Guid.NewGuid(),
            SpecificationId = Guid.Empty,
            CurrentUserId = Guid.NewGuid(),
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
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
    public async Task HandleAsync_Should_CallGateService_ToRequireApprovedOrder()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupNoExistingTestCases();
        SetupSubscriptionAllowed();
        SetupGeneratorReturnsEmpty();

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _gateServiceMock.Verify(x => x.RequireApprovedOrderAsync(suite.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenExistingCasesAndNoForceRegenerate()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupExistingHappyPathCases(3);
        SetupSubscriptionAllowed();

        var command = CreateValidCommand(suite.CreatedById);
        command.ForceRegenerate = false;

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*ForceRegenerate*");
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenSubscriptionLimitExceeded()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupNoExistingTestCases();
        SetupSubscriptionDenied();

        var command = CreateValidCommand(suite.CreatedById);

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*subscription*");
    }

    [Fact]
    public async Task HandleAsync_Should_GenerateAndPersistTestCases_WhenValid()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupNoExistingTestCases();
        SetupSubscriptionAllowed();

        var generatedCases = CreateGeneratedTestCases(2);
        SetupGeneratorReturns(generatedCases);

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        // Verify test cases were added
        _testCaseRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCase>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _requestRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _expectationRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseExpectation>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _changeLogRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseChangeLog>(), It.IsAny<CancellationToken>()), Times.Exactly(2));

        // Verify suite version was created
        _versionRepoMock.Verify(x => x.AddAsync(It.IsAny<TestSuiteVersion>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify suite was updated
        _suiteRepoMock.Verify(x => x.UpdateAsync(It.IsAny<TestSuite>(), It.IsAny<CancellationToken>()), Times.Once);

        // Verify result model
        command.Result.Should().NotBeNull();
        command.Result.TotalGenerated.Should().Be(2);
        command.Result.TestSuiteId.Should().Be(suite.Id);
    }

    [Fact]
    public async Task HandleAsync_Should_DeleteExistingCases_WhenForceRegenerate()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupExistingHappyPathCases(2);
        SetupSubscriptionAllowed();
        SetupGeneratorReturnsEmpty();

        var command = CreateValidCommand(suite.CreatedById);
        command.ForceRegenerate = true;

        await _handler.HandleAsync(command);

        // Verify existing cases were deleted
        _testCaseRepoMock.Verify(x => x.Delete(It.IsAny<TestCase>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_Should_IncrementSubscriptionUsage_AfterPersistence()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupNoExistingTestCases();
        SetupSubscriptionAllowed();

        var generatedCases = CreateGeneratedTestCases(3);
        SetupGeneratorReturns(generatedCases);

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _subscriptionMock.Verify(x => x.IncrementUsageAsync(
            It.Is<IncrementUsageRequest>(r =>
                r.LimitType == LimitType.MaxTestCasesPerSuite && r.IncrementValue == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_SetResultWithEmptyCases_WhenGeneratorReturnsNone()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupNoExistingTestCases();
        SetupSubscriptionAllowed();
        SetupGeneratorReturnsEmpty();

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        command.Result.Should().NotBeNull();
        command.Result.TotalGenerated.Should().Be(0);
        command.Result.TestCases.Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_Should_PersistVariablesForGeneratedCases()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupNoExistingTestCases();
        SetupSubscriptionAllowed();

        // Generate 1 test case with 2 variables
        var testCase = new TestCase
        {
            Id = Guid.NewGuid(),
            TestSuiteId = suite.Id,
            Name = "Test with vars",
            TestType = TestType.HappyPath,
            Request = new TestCaseRequest { Id = Guid.NewGuid(), HttpMethod = HttpMethodEnum.POST },
            Expectation = new TestCaseExpectation { Id = Guid.NewGuid() },
            Variables = new List<TestCaseVariable>
            {
                new() { Id = Guid.NewGuid(), VariableName = "token", ExtractFrom = ExtractFrom.ResponseBody },
                new() { Id = Guid.NewGuid(), VariableName = "userId", ExtractFrom = ExtractFrom.ResponseBody },
            },
        };

        _generatorMock.Setup(x => x.GenerateAsync(
                It.IsAny<TestSuite>(),
                It.IsAny<IReadOnlyList<ApiOrderItemModel>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HappyPathGenerationResult
            {
                TestCases = new List<TestCase> { testCase },
                EndpointsCovered = 1,
                LlmModel = "gpt-4",
            });

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _variableRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseVariable>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_Should_SetSuiteStatusToReady_WhenGenerationSucceeds()
    {
        var suite = CreateSuite();
        suite.Status = TestSuiteStatus.Draft; // Start as Draft
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupNoExistingTestCases();
        SetupSubscriptionAllowed();

        var generatedCases = CreateGeneratedTestCases(2);
        SetupGeneratorReturns(generatedCases);

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        // Verify suite status was set to Ready
        suite.Status.Should().Be(TestSuiteStatus.Ready);
        _suiteRepoMock.Verify(x => x.UpdateAsync(
            It.Is<TestSuite>(s => s.Status == TestSuiteStatus.Ready),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_PersistDependencies_WhenTestCasesHaveDependencies()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupNoExistingTestCases();
        SetupSubscriptionAllowed();

        // Generate 1 test case with 2 dependencies
        var testCaseId = Guid.NewGuid();
        var testCase = new TestCase
        {
            Id = testCaseId,
            TestSuiteId = suite.Id,
            Name = "Test with deps",
            TestType = TestType.HappyPath,
            Request = new TestCaseRequest { Id = Guid.NewGuid(), HttpMethod = HttpMethodEnum.POST },
            Expectation = new TestCaseExpectation { Id = Guid.NewGuid() },
            Variables = new List<TestCaseVariable>(),
            Dependencies = new List<TestCaseDependency>
            {
                new() { Id = Guid.NewGuid(), TestCaseId = testCaseId, DependsOnTestCaseId = Guid.NewGuid() },
                new() { Id = Guid.NewGuid(), TestCaseId = testCaseId, DependsOnTestCaseId = Guid.NewGuid() },
                new() { Id = Guid.NewGuid(), TestCaseId = testCaseId, DependsOnTestCaseId = Guid.NewGuid() },
            },
        };

        _generatorMock.Setup(x => x.GenerateAsync(
                It.IsAny<TestSuite>(),
                It.IsAny<IReadOnlyList<ApiOrderItemModel>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HappyPathGenerationResult
            {
                TestCases = new List<TestCase> { testCase },
                EndpointsCovered = 1,
                LlmModel = "gpt-4",
            });

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        // Verify all 3 dependencies were persisted
        _dependencyRepoMock.Verify(x => x.AddAsync(It.IsAny<TestCaseDependency>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    #region Helpers

    private static readonly Guid DefaultUserId = Guid.NewGuid();
    private static readonly Guid DefaultSuiteId = Guid.NewGuid();
    private static readonly Guid DefaultSpecId = Guid.NewGuid();

    private static TestSuite CreateSuite()
    {
        return new TestSuite
        {
            Id = DefaultSuiteId,
            CreatedById = DefaultUserId,
            Name = "Test Suite FE-05B",
            Status = TestSuiteStatus.Ready,
            ApprovalStatus = ApprovalStatus.Approved,
            Version = 1,
            EndpointBusinessContexts = new Dictionary<Guid, string>(),
        };
    }

    private static GenerateHappyPathTestCasesCommand CreateValidCommand(Guid? userId = null)
    {
        return new GenerateHappyPathTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            SpecificationId = DefaultSpecId,
            CurrentUserId = userId ?? DefaultUserId,
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

    private void SetupGateApproved(Guid suiteId)
    {
        var approvedOrder = new List<ApiOrderItemModel>
        {
            new() { EndpointId = Guid.NewGuid(), HttpMethod = "POST", Path = "/api/auth/login", OrderIndex = 0, IsAuthRelated = true },
            new() { EndpointId = Guid.NewGuid(), HttpMethod = "GET", Path = "/api/users", OrderIndex = 1 },
        };
        _gateServiceMock.Setup(x => x.RequireApprovedOrderAsync(suiteId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approvedOrder);
    }

    private void SetupNoExistingTestCases()
    {
        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(new List<TestCase>());
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(new List<TestCase>().AsQueryable());
    }

    private void SetupExistingHappyPathCases(int count)
    {
        var existing = Enumerable.Range(0, count)
            .Select(i => new TestCase
            {
                Id = Guid.NewGuid(),
                TestSuiteId = DefaultSuiteId,
                TestType = TestType.HappyPath,
            })
            .ToList();

        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(existing);
        _testCaseRepoMock.Setup(x => x.GetQueryableSet())
            .Returns(existing.AsQueryable());
    }

    private void SetupSubscriptionAllowed()
    {
        _subscriptionMock.Setup(x => x.CheckLimitAsync(
                It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });
    }

    private void SetupSubscriptionDenied()
    {
        _subscriptionMock.Setup(x => x.CheckLimitAsync(
                It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = false, DenialReason = "Limit exceeded" });
    }

    private void SetupGeneratorReturnsEmpty()
    {
        _generatorMock.Setup(x => x.GenerateAsync(
                It.IsAny<TestSuite>(),
                It.IsAny<IReadOnlyList<ApiOrderItemModel>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HappyPathGenerationResult
            {
                TestCases = Array.Empty<TestCase>(),
                EndpointsCovered = 0,
            });
    }

    private void SetupGeneratorReturns(IReadOnlyList<TestCase> testCases)
    {
        _generatorMock.Setup(x => x.GenerateAsync(
                It.IsAny<TestSuite>(),
                It.IsAny<IReadOnlyList<ApiOrderItemModel>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HappyPathGenerationResult
            {
                TestCases = testCases,
                EndpointsCovered = testCases.Select(tc => tc.EndpointId).Distinct().Count(),
                LlmModel = "gpt-4",
                TokensUsed = 1500,
            });
    }

    private static IReadOnlyList<TestCase> CreateGeneratedTestCases(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new TestCase
            {
                Id = Guid.NewGuid(),
                TestSuiteId = DefaultSuiteId,
                EndpointId = Guid.NewGuid(),
                Name = $"Test Case {i}",
                TestType = TestType.HappyPath,
                OrderIndex = i,
                Request = new TestCaseRequest
                {
                    Id = Guid.NewGuid(),
                    HttpMethod = HttpMethodEnum.GET,
                    Url = $"/api/resource/{i}",
                },
                Expectation = new TestCaseExpectation
                {
                    Id = Guid.NewGuid(),
                    ExpectedStatus = "[200]",
                },
                Variables = new List<TestCaseVariable>(),
            })
            .ToList();
    }

    #endregion
}
