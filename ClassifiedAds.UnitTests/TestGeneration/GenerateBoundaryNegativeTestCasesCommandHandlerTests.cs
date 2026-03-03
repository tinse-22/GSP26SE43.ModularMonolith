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
/// Unit tests for GenerateBoundaryNegativeTestCasesCommandHandler.
/// Verifies gate check, subscription limits, LLM call limits, force-regenerate,
/// include-flag validation, entity persistence, and error handling for the
/// boundary/negative test case generation pipeline.
/// </summary>
public class GenerateBoundaryNegativeTestCasesCommandHandlerTests
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
    private readonly Mock<IBoundaryNegativeTestCaseGenerator> _generatorMock;
    private readonly Mock<ISubscriptionLimitGatewayService> _subscriptionMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly GenerateBoundaryNegativeTestCasesCommandHandler _handler;

    public GenerateBoundaryNegativeTestCasesCommandHandlerTests()
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
        _generatorMock = new Mock<IBoundaryNegativeTestCaseGenerator>();
        _subscriptionMock = new Mock<ISubscriptionLimitGatewayService>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();

        _testCaseRepoMock.Setup(x => x.UnitOfWork).Returns(_unitOfWorkMock.Object);

        _unitOfWorkMock.Setup(x => x.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, IsolationLevel, CancellationToken>(
                async (operation, _, ct) => await operation(ct));

        _handler = new GenerateBoundaryNegativeTestCasesCommandHandler(
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
            new Mock<ILogger<GenerateBoundaryNegativeTestCasesCommandHandler>>().Object);
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenTestSuiteIdEmpty()
    {
        var command = new GenerateBoundaryNegativeTestCasesCommand
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
        var command = new GenerateBoundaryNegativeTestCasesCommand
        {
            TestSuiteId = Guid.NewGuid(),
            SpecificationId = Guid.Empty,
            CurrentUserId = Guid.NewGuid(),
        };

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HandleAsync_Should_ThrowValidation_WhenNoIncludeFlagsSet()
    {
        var command = new GenerateBoundaryNegativeTestCasesCommand
        {
            TestSuiteId = Guid.NewGuid(),
            SpecificationId = Guid.NewGuid(),
            CurrentUserId = Guid.NewGuid(),
            IncludePathMutations = false,
            IncludeBodyMutations = false,
            IncludeLlmSuggestions = false,
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
    public async Task HandleAsync_Should_CallGateService()
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
        SetupExistingBoundaryNegativeCases(3);
        SetupSubscriptionAllowed();

        var command = CreateValidCommand(suite.CreatedById);
        command.ForceRegenerate = false;

        var act = () => _handler.HandleAsync(command);
        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*ForceRegenerate*");
    }

    [Fact]
    public async Task HandleAsync_Should_DeleteExistingCases_WhenForceRegenerate()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupExistingBoundaryNegativeCases(2);
        SetupSubscriptionAllowed();
        SetupGeneratorReturnsEmpty();

        var command = CreateValidCommand(suite.CreatedById);
        command.ForceRegenerate = true;

        await _handler.HandleAsync(command);

        // Verify existing cases were deleted
        _testCaseRepoMock.Verify(x => x.Delete(It.IsAny<TestCase>()), Times.Exactly(2));
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
    public async Task HandleAsync_Should_CheckLlmCallLimit_WhenIncludeLlmSuggestions()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupNoExistingTestCases();
        SetupSubscriptionAllowed();
        SetupGeneratorReturnsEmpty();

        var command = CreateValidCommand(suite.CreatedById);
        command.IncludeLlmSuggestions = true;

        await _handler.HandleAsync(command);

        _subscriptionMock.Verify(x => x.CheckLimitAsync(
            command.CurrentUserId,
            LimitType.MaxLlmCallsPerMonth,
            It.IsAny<decimal>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_GenerateAndPersistTestCases()
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
    public async Task HandleAsync_Should_IncrementSubscriptionUsage()
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
    public async Task HandleAsync_Should_CreateSuiteVersion()
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

        _versionRepoMock.Verify(x => x.AddAsync(
            It.Is<TestSuiteVersion>(v =>
                v.TestSuiteId == suite.Id &&
                v.ChangedById == command.CurrentUserId &&
                v.ChangeType == VersionChangeType.TestCasesModified),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_Should_CreateChangeLog_ForEachCase()
    {
        var suite = CreateSuite();
        SetupSuiteFound(suite);
        SetupGateApproved(suite.Id);
        SetupNoExistingTestCases();
        SetupSubscriptionAllowed();

        var generatedCases = CreateGeneratedTestCases(4);
        SetupGeneratorReturns(generatedCases);

        var command = CreateValidCommand(suite.CreatedById);
        await _handler.HandleAsync(command);

        _changeLogRepoMock.Verify(x => x.AddAsync(
            It.Is<TestCaseChangeLog>(cl =>
                cl.ChangeType == TestCaseChangeType.Created &&
                cl.ChangedById == command.CurrentUserId),
            It.IsAny<CancellationToken>()), Times.Exactly(4));
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
            Name = "Test Suite Boundary/Negative",
            Status = TestSuiteStatus.Ready,
            ApprovalStatus = ApprovalStatus.Approved,
            Version = 1,
            EndpointBusinessContexts = new Dictionary<Guid, string>(),
        };
    }

    private static GenerateBoundaryNegativeTestCasesCommand CreateValidCommand(Guid? userId = null)
    {
        return new GenerateBoundaryNegativeTestCasesCommand
        {
            TestSuiteId = DefaultSuiteId,
            SpecificationId = DefaultSpecId,
            CurrentUserId = userId ?? DefaultUserId,
            IncludePathMutations = true,
            IncludeBodyMutations = true,
            IncludeLlmSuggestions = true,
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

    private void SetupExistingBoundaryNegativeCases(int count)
    {
        var existing = Enumerable.Range(0, count)
            .Select(i => new TestCase
            {
                Id = Guid.NewGuid(),
                TestSuiteId = DefaultSuiteId,
                TestType = i % 2 == 0 ? TestType.Boundary : TestType.Negative,
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
                It.IsAny<BoundaryNegativeOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoundaryNegativeGenerationResult
            {
                TestCases = Array.Empty<TestCase>(),
                PathMutationCount = 0,
                BodyMutationCount = 0,
                LlmSuggestionCount = 0,
                EndpointsCovered = 0,
            });
    }

    private void SetupGeneratorReturns(IReadOnlyList<TestCase> testCases)
    {
        _generatorMock.Setup(x => x.GenerateAsync(
                It.IsAny<TestSuite>(),
                It.IsAny<IReadOnlyList<ApiOrderItemModel>>(),
                It.IsAny<Guid>(),
                It.IsAny<BoundaryNegativeOptions>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BoundaryNegativeGenerationResult
            {
                TestCases = testCases,
                PathMutationCount = testCases.Count(tc => tc.TestType == TestType.Boundary),
                BodyMutationCount = testCases.Count(tc => tc.TestType == TestType.Negative),
                LlmSuggestionCount = 1,
                EndpointsCovered = testCases.Select(tc => tc.EndpointId).Distinct().Count(),
                LlmModel = "gpt-4",
                LlmTokensUsed = 1500,
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
                Name = $"Boundary/Negative Test Case {i}",
                TestType = i % 2 == 0 ? TestType.Boundary : TestType.Negative,
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
                    ExpectedStatus = "[400]",
                },
                Variables = new List<TestCaseVariable>(),
                Dependencies = new List<TestCaseDependency>(),
            })
            .ToList();
    }

    #endregion
}
