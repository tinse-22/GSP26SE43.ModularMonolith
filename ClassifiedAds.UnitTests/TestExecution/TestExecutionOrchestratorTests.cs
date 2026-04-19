using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestExecution;

public class TestExecutionOrchestratorTests
{
    private readonly Mock<IRepository<TestRun, Guid>> _runRepoMock;
    private readonly Mock<IRepository<ExecutionEnvironment, Guid>> _envRepoMock;
    private readonly Mock<ITestExecutionReadGatewayService> _gatewayMock;
    private readonly Mock<IApiEndpointMetadataService> _endpointMetadataMock;
    private readonly Mock<ISubscriptionLimitGatewayService> _limitServiceMock;
    private readonly Mock<IExecutionEnvironmentRuntimeResolver> _envResolverMock;
    private readonly Mock<IVariableResolver> _variableResolverMock;
    private readonly Mock<IHttpTestExecutor> _httpExecutorMock;
    private readonly Mock<IVariableExtractor> _variableExtractorMock;
    private readonly Mock<IRuleBasedValidator> _validatorMock;
    private readonly Mock<ITestResultCollector> _resultCollectorMock;
    private readonly Mock<IPreExecutionValidator> _preValidatorMock;
    private readonly TestExecutionOrchestrator _orchestrator;

    private readonly Guid _runId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _suiteId = Guid.NewGuid();
    private readonly Guid _envId = Guid.NewGuid();
    private readonly Guid _specId = Guid.NewGuid();

    public TestExecutionOrchestratorTests()
    {
        _runRepoMock = new Mock<IRepository<TestRun, Guid>>();
        _envRepoMock = new Mock<IRepository<ExecutionEnvironment, Guid>>();
        _gatewayMock = new Mock<ITestExecutionReadGatewayService>();
        _endpointMetadataMock = new Mock<IApiEndpointMetadataService>();
        _limitServiceMock = new Mock<ISubscriptionLimitGatewayService>();
        _envResolverMock = new Mock<IExecutionEnvironmentRuntimeResolver>();
        _variableResolverMock = new Mock<IVariableResolver>();
        _httpExecutorMock = new Mock<IHttpTestExecutor>();
        _variableExtractorMock = new Mock<IVariableExtractor>();
        _validatorMock = new Mock<IRuleBasedValidator>();
        _resultCollectorMock = new Mock<ITestResultCollector>();
        _preValidatorMock = new Mock<IPreExecutionValidator>();

        // Default: pre-validation always passes
        _preValidatorMock
            .Setup(x => x.Validate(
                It.IsAny<ExecutionTestCaseDto>(),
                It.IsAny<ResolvedExecutionEnvironment>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<ApiEndpointMetadataDto>()))
            .Returns(new PreExecutionValidationResult());

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        _runRepoMock.Setup(x => x.UnitOfWork).Returns(unitOfWorkMock.Object);
        _runRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestRun>().AsQueryable());
        _envRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<ExecutionEnvironment>().AsQueryable());

        _orchestrator = new TestExecutionOrchestrator(
            _runRepoMock.Object,
            _envRepoMock.Object,
            _gatewayMock.Object,
            _endpointMetadataMock.Object,
            _limitServiceMock.Object,
            _envResolverMock.Object,
            _variableResolverMock.Object,
            _httpExecutorMock.Object,
            _variableExtractorMock.Object,
            _validatorMock.Object,
            _resultCollectorMock.Object,
            _preValidatorMock.Object,
            new Mock<ILogger<TestExecutionOrchestrator>>().Object);
    }

    [Fact]
    public async Task ExecuteAsync_Should_SkipTestCase_WhenDependencyFailed()
    {
        // Arrange
        var caseAId = Guid.NewGuid();
        var caseBId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var caseA = CreateTestCase(caseAId, endpointId, 0);
        var caseB = CreateTestCase(caseBId, endpointId, 1, dependencyIds: new[] { caseAId });

        SetupDefaultMocks(new[] { caseA, caseB }, new[] { endpointId });

        // Case A: fails with non-usable result (5xx + non-status-mismatch failure)
        SetupTestCaseExecution(caseA, isPassed: false);
        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(It.Is<ResolvedTestCaseRequest>(r => r.TestCaseId == caseA.TestCaseId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 500,
                Body = "{}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 40,
            });
        _validatorMock
            .Setup(x => x.Validate(It.IsAny<HttpTestResponse>(), It.Is<ExecutionTestCaseDto>(tc => tc.TestCaseId == caseA.TestCaseId), It.IsAny<ApiEndpointMetadataDto>()))
            .Returns(new TestCaseValidationResult
            {
                IsPassed = false,
                StatusCodeMatched = false,
                Failures = new List<ValidationFailureModel>
                {
                    new() { Code = "BODY_CONTAINS_MISSING", Message = "Hard failure" },
                },
            });

        // Case B: would normally pass, but should be skipped
        SetupTestCaseExecution(caseB, isPassed: true);

        TestRunResultModel? capturedResult = null;
        _resultCollectorMock
            .Setup(x => x.CollectAsync(It.IsAny<TestRun>(), It.IsAny<IReadOnlyList<TestCaseExecutionResult>>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TestRun, IReadOnlyList<TestCaseExecutionResult>, int, string, CancellationToken>((_, results, _, _, _) =>
            {
                capturedResult = new TestRunResultModel
                {
                    Cases = results.Select(r => new TestCaseRunResultModel { TestCaseId = r.TestCaseId, Status = r.Status }).ToList(),
                };
            })
            .ReturnsAsync(() => capturedResult!);

        // Act
        var result = await _orchestrator.ExecuteAsync(_runId, _userId, Array.Empty<Guid>());

        // Assert
        result.Cases.Should().HaveCount(2);
        result.Cases[0].Status.Should().Be("Failed");
        result.Cases[1].Status.Should().Be("Skipped");
    }

    [Fact]
    public async Task ExecuteAsync_Should_NotSkipDependency_WhenOnlyStatusMismatchBut2xx()
    {
        // Arrange
        var caseAId = Guid.NewGuid();
        var caseBId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var caseA = CreateTestCase(caseAId, endpointId, 0);
        var caseB = CreateTestCase(caseBId, endpointId, 1, dependencyIds: new[] { caseAId });

        SetupDefaultMocks(new[] { caseA, caseB }, new[] { endpointId });

        _variableResolverMock
            .Setup(x => x.Resolve(It.IsAny<ExecutionTestCaseDto>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<ResolvedExecutionEnvironment>()))
            .Returns<ExecutionTestCaseDto, IReadOnlyDictionary<string, string>, ResolvedExecutionEnvironment>((tc, _, _) => new ResolvedTestCaseRequest
            {
                TestCaseId = tc.TestCaseId,
                Name = tc.Name,
                HttpMethod = "POST",
                ResolvedUrl = "https://api.example.com/test",
                TimeoutMs = 30000,
            });

        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(It.Is<ResolvedTestCaseRequest>(r => r.TestCaseId == caseAId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 201,
                Body = "{}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 30,
            });
        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(It.Is<ResolvedTestCaseRequest>(r => r.TestCaseId == caseBId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 200,
                Body = "{}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 30,
            });

        _variableExtractorMock
            .Setup(x => x.Extract(It.IsAny<HttpTestResponse>(), It.IsAny<IReadOnlyList<ExecutionVariableRuleDto>>()))
            .Returns(new Dictionary<string, string>());

        _validatorMock
            .Setup(x => x.Validate(It.IsAny<HttpTestResponse>(), It.Is<ExecutionTestCaseDto>(tc => tc.TestCaseId == caseAId), It.IsAny<ApiEndpointMetadataDto>()))
            .Returns(new TestCaseValidationResult
            {
                IsPassed = false,
                StatusCodeMatched = false,
                Failures = new List<ValidationFailureModel>
                {
                    new() { Code = "STATUS_CODE_MISMATCH", Message = "Expected 200 but got 201" },
                },
            });
        _validatorMock
            .Setup(x => x.Validate(It.IsAny<HttpTestResponse>(), It.Is<ExecutionTestCaseDto>(tc => tc.TestCaseId == caseBId), It.IsAny<ApiEndpointMetadataDto>()))
            .Returns(new TestCaseValidationResult
            {
                IsPassed = true,
                StatusCodeMatched = true,
                Failures = new List<ValidationFailureModel>(),
            });

        TestRunResultModel? capturedResult = null;
        _resultCollectorMock
            .Setup(x => x.CollectAsync(It.IsAny<TestRun>(), It.IsAny<IReadOnlyList<TestCaseExecutionResult>>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TestRun, IReadOnlyList<TestCaseExecutionResult>, int, string, CancellationToken>((_, results, _, _, _) =>
            {
                capturedResult = new TestRunResultModel
                {
                    Cases = results.Select(r => new TestCaseRunResultModel { TestCaseId = r.TestCaseId, Status = r.Status }).ToList(),
                };
            })
            .ReturnsAsync(() => capturedResult!);

        // Act
        var result = await _orchestrator.ExecuteAsync(_runId, _userId, Array.Empty<Guid>());

        // Assert
        result.Cases.Should().HaveCount(2);
        result.Cases[0].Status.Should().Be("Failed");
        result.Cases[1].Status.Should().Be("Passed");
    }

    [Fact]
    public async Task ExecuteAsync_Should_FetchEndpointMetadataOnce()
    {
        // Arrange
        var endpoint1 = Guid.NewGuid();
        var endpoint2 = Guid.NewGuid();
        var case1 = CreateTestCase(Guid.NewGuid(), endpoint1, 0);
        var case2 = CreateTestCase(Guid.NewGuid(), endpoint2, 1);

        SetupDefaultMocks(new[] { case1, case2 }, new[] { endpoint1, endpoint2 });
        SetupTestCaseExecution(case1, isPassed: true);
        SetupTestCaseExecution(case2, isPassed: true);
        SetupResultCollector();

        // Act
        await _orchestrator.ExecuteAsync(_runId, _userId, Array.Empty<Guid>());

        // Assert — endpoint metadata fetched exactly once
        _endpointMetadataMock.Verify(
            x => x.GetEndpointMetadataAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_ResolveEnvironmentOnce()
    {
        // Arrange
        var case1 = CreateTestCase(Guid.NewGuid(), Guid.NewGuid(), 0);
        var case2 = CreateTestCase(Guid.NewGuid(), Guid.NewGuid(), 1);

        SetupDefaultMocks(new[] { case1, case2 }, new[] { case1.EndpointId.Value, case2.EndpointId.Value });
        SetupTestCaseExecution(case1, isPassed: true);
        SetupTestCaseExecution(case2, isPassed: true);
        SetupResultCollector();

        // Act
        await _orchestrator.ExecuteAsync(_runId, _userId, Array.Empty<Guid>());

        // Assert — environment resolved exactly once
        _envResolverMock.Verify(
            x => x.ResolveAsync(It.IsAny<ExecutionEnvironment>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Should_MarkRunAsRunning()
    {
        // Arrange
        var case1 = CreateTestCase(Guid.NewGuid(), Guid.NewGuid(), 0);
        SetupDefaultMocks(new[] { case1 }, new[] { case1.EndpointId.Value });
        SetupTestCaseExecution(case1, isPassed: true);
        SetupResultCollector();

        TestRun? updatedRun = null;
        _runRepoMock
            .Setup(x => x.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .Callback<TestRun, CancellationToken>((run, _) =>
            {
                if (run.Status == TestRunStatus.Running)
                {
                    updatedRun = run;
                }
            })
            .Returns(Task.CompletedTask);

        // Act
        await _orchestrator.ExecuteAsync(_runId, _userId, Array.Empty<Guid>());

        // Assert
        updatedRun.Should().NotBeNull();
        updatedRun.Status.Should().Be(TestRunStatus.Running);
        updatedRun.StartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_Should_AccumulateExtractedVariables()
    {
        // Arrange
        var case1Id = Guid.NewGuid();
        var case2Id = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var case1 = CreateTestCase(case1Id, endpointId, 0);
        var case2 = CreateTestCase(case2Id, endpointId, 1);

        SetupDefaultMocks(new[] { case1, case2 }, new[] { endpointId });

        // Track variables received by each Resolve call
        Dictionary<string, string>? case2Variables = null;

        _variableResolverMock
            .Setup(x => x.Resolve(It.IsAny<ExecutionTestCaseDto>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<ResolvedExecutionEnvironment>()))
            .Returns<ExecutionTestCaseDto, IReadOnlyDictionary<string, string>, ResolvedExecutionEnvironment>((tc, vars, _) =>
            {
                if (tc.TestCaseId == case2Id)
                {
                    case2Variables = new Dictionary<string, string>(vars);
                }

                return new ResolvedTestCaseRequest
                {
                    TestCaseId = tc.TestCaseId,
                    Name = tc.Name,
                    HttpMethod = "GET",
                    ResolvedUrl = "https://api.example.com/test",
                    TimeoutMs = 30000,
                };
            });

        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ResolvedTestCaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 200,
                Body = "{\"token\": \"extracted-token\"}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 50,
            });

        // Case 1 extracts "token", case 2 extracts nothing
        var extractCallCount = 0;
        _variableExtractorMock
            .Setup(x => x.Extract(It.IsAny<HttpTestResponse>(), It.IsAny<IReadOnlyList<ExecutionVariableRuleDto>>()))
            .Returns<HttpTestResponse, IReadOnlyList<ExecutionVariableRuleDto>>((_, _) =>
            {
                extractCallCount++;
                return extractCallCount == 1
                    ? new Dictionary<string, string> { ["token"] = "extracted-token" }
                    : new Dictionary<string, string>();
            });

        _validatorMock
            .Setup(x => x.Validate(It.IsAny<HttpTestResponse>(), It.IsAny<ExecutionTestCaseDto>(), It.IsAny<ApiEndpointMetadataDto>()))
            .Returns(new TestCaseValidationResult { IsPassed = true, StatusCodeMatched = true });

        SetupResultCollector();

        // Act
        await _orchestrator.ExecuteAsync(_runId, _userId, Array.Empty<Guid>());

        // Assert — variables extracted from case 1 are available to case 2
        case2Variables.Should().NotBeNull();
        case2Variables.Should().ContainKey("token");
        case2Variables["token"].Should().Be("extracted-token");
    }

    [Fact]
    public async Task ExecuteAsync_Should_ChainAuthTokenThroughMultipleCrudRequests()
    {
        // Arrange — 3 sequential test cases:
        // Case 1: Login → extracts accessToken and userId
        // Case 2: Create resource → uses accessToken in header, extracts resourceId
        // Case 3: Get resource → uses accessToken + resourceId
        var loginId = Guid.NewGuid();
        var createId = Guid.NewGuid();
        var getId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var loginCase = CreateTestCase(loginId, endpointId, 0);
        var createCase = CreateTestCase(createId, endpointId, 1, dependencyIds: new[] { loginId });
        var getCase = CreateTestCase(getId, endpointId, 2, dependencyIds: new[] { loginId, createId });

        SetupDefaultMocks(new[] { loginCase, createCase, getCase }, new[] { endpointId });

        // Track variable bags received by each Resolve call
        Dictionary<string, string>? createVars = null;
        Dictionary<string, string>? getVars = null;

        _variableResolverMock
            .Setup(x => x.Resolve(It.IsAny<ExecutionTestCaseDto>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<ResolvedExecutionEnvironment>()))
            .Returns<ExecutionTestCaseDto, IReadOnlyDictionary<string, string>, ResolvedExecutionEnvironment>((tc, vars, _) =>
            {
                if (tc.TestCaseId == createId)
                {
                    createVars = new Dictionary<string, string>(vars);
                }
                else if (tc.TestCaseId == getId)
                {
                    getVars = new Dictionary<string, string>(vars);
                }

                return new ResolvedTestCaseRequest
                {
                    TestCaseId = tc.TestCaseId,
                    Name = tc.Name,
                    HttpMethod = "POST",
                    ResolvedUrl = "https://api.example.com/test",
                    TimeoutMs = 30000,
                };
            });

        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ResolvedTestCaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 200,
                Body = "{}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 30,
            });

        // Login extracts accessToken + userId
        // Create extracts resourceId
        // Get extracts nothing
        var callIndex = 0;
        _variableExtractorMock
            .Setup(x => x.Extract(It.IsAny<HttpTestResponse>(), It.IsAny<IReadOnlyList<ExecutionVariableRuleDto>>()))
            .Returns<HttpTestResponse, IReadOnlyList<ExecutionVariableRuleDto>>((_, _) =>
            {
                callIndex++;
                return callIndex switch
                {
                    1 => new Dictionary<string, string>
                    {
                        ["accessToken"] = "jwt-abc-123",
                        ["userId"] = "user-42",
                    },
                    2 => new Dictionary<string, string>
                    {
                        ["resourceId"] = "res-789",
                    },
                    _ => new Dictionary<string, string>(),
                };
            });

        _validatorMock
            .Setup(x => x.Validate(It.IsAny<HttpTestResponse>(), It.IsAny<ExecutionTestCaseDto>(), It.IsAny<ApiEndpointMetadataDto>()))
            .Returns(new TestCaseValidationResult { IsPassed = true, StatusCodeMatched = true });

        SetupResultCollector();

        // Act
        await _orchestrator.ExecuteAsync(_runId, _userId, Array.Empty<Guid>());

        // Assert — Create case receives both login-extracted variables
        createVars.Should().NotBeNull();
        createVars.Should().ContainKey("accessToken").WhoseValue.Should().Be("jwt-abc-123");
        createVars.Should().ContainKey("userId").WhoseValue.Should().Be("user-42");
        createVars.Should().NotContainKey("resourceId"); // not yet extracted

        // Assert — Get case receives ALL accumulated variables (login + create)
        getVars.Should().NotBeNull();
        getVars.Should().ContainKey("accessToken").WhoseValue.Should().Be("jwt-abc-123");
        getVars.Should().ContainKey("userId").WhoseValue.Should().Be("user-42");
        getVars.Should().ContainKey("resourceId").WhoseValue.Should().Be("res-789");
    }

    [Fact]
    public async Task ExecuteAsync_Should_FailCase_WhenUnresolvedVariable()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var testCase = CreateTestCase(caseId, endpointId, 0);

        SetupDefaultMocks(new[] { testCase }, new[] { endpointId });

        // Variable resolver throws UnresolvedVariableException
        _variableResolverMock
            .Setup(x => x.Resolve(It.IsAny<ExecutionTestCaseDto>(), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<ResolvedExecutionEnvironment>()))
            .Throws(new UnresolvedVariableException("Bien '{{missing}}' chua duoc giai quyet trong URL."));

        TestCaseExecutionResult? capturedFailedCase = null;
        _resultCollectorMock
            .Setup(x => x.CollectAsync(It.IsAny<TestRun>(), It.IsAny<IReadOnlyList<TestCaseExecutionResult>>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<TestRun, IReadOnlyList<TestCaseExecutionResult>, int, string, CancellationToken>((_, results, _, _, _) =>
            {
                capturedFailedCase = results.FirstOrDefault();
            })
            .ReturnsAsync(new TestRunResultModel());

        // Act
        await _orchestrator.ExecuteAsync(_runId, _userId, Array.Empty<Guid>());

        // Assert
        capturedFailedCase.Should().NotBeNull();
        capturedFailedCase.Status.Should().Be("Failed");
        capturedFailedCase.FailureReasons.Should().ContainSingle(f => f.Code == "UNRESOLVED_VARIABLE");
    }

    #region Setup Helpers

    private void SetupDefaultMocks(ExecutionTestCaseDto[] testCases, Guid[] endpointIds)
    {
        // TestRun entity
        var run = new TestRun
        {
            Id = _runId,
            TestSuiteId = _suiteId,
            EnvironmentId = _envId,
            TriggeredById = _userId,
            Status = TestRunStatus.Pending,
        };
        _runRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestRun>>()))
            .ReturnsAsync(run);
        _runRepoMock.Setup(x => x.UpdateAsync(It.IsAny<TestRun>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // ExecutionEnvironment entity
        var env = new ExecutionEnvironment
        {
            Id = _envId,
            ProjectId = Guid.NewGuid(),
            Name = "Test Env",
            BaseUrl = "https://api.example.com",
        };
        _envRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<ExecutionEnvironment>>()))
            .ReturnsAsync(env);

        // Gateway returns execution context
        _gatewayMock
            .Setup(x => x.GetExecutionContextAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteExecutionContextDto
            {
                Suite = new TestSuiteAccessContextDto
                {
                    TestSuiteId = _suiteId,
                    ApiSpecId = _specId,
                    Status = "Ready",
                },
                OrderedTestCases = testCases,
                OrderedEndpointIds = endpointIds,
            });

        // Environment resolver
        _envResolverMock
            .Setup(x => x.ResolveAsync(It.IsAny<ExecutionEnvironment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ResolvedExecutionEnvironment
            {
                EnvironmentId = _envId,
                Name = "Test Env",
                BaseUrl = "https://api.example.com",
                Variables = new Dictionary<string, string>(),
                DefaultHeaders = new Dictionary<string, string>(),
                DefaultQueryParams = new Dictionary<string, string>(),
            });

        // Subscription limit
        _limitServiceMock
            .Setup(x => x.CheckLimitAsync(It.IsAny<Guid>(), LimitType.RetentionDays, It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true, LimitValue = 7 });

        // Endpoint metadata
        _endpointMetadataMock
            .Setup(x => x.GetEndpointMetadataAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(endpointIds.Select(id => new ApiEndpointMetadataDto { EndpointId = id }).ToList());
    }

    private void SetupTestCaseExecution(ExecutionTestCaseDto testCase, bool isPassed)
    {
        // Default variable resolver setup (only if no specific overrides)
        _variableResolverMock
            .Setup(x => x.Resolve(It.Is<ExecutionTestCaseDto>(tc => tc.TestCaseId == testCase.TestCaseId), It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<ResolvedExecutionEnvironment>()))
            .Returns(new ResolvedTestCaseRequest
            {
                TestCaseId = testCase.TestCaseId,
                Name = testCase.Name,
                HttpMethod = "GET",
                ResolvedUrl = "https://api.example.com/test",
                TimeoutMs = 30000,
            });

        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(It.Is<ResolvedTestCaseRequest>(r => r.TestCaseId == testCase.TestCaseId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 200,
                Body = "{}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 50,
            });

        _variableExtractorMock
            .Setup(x => x.Extract(It.IsAny<HttpTestResponse>(), testCase.Variables))
            .Returns(new Dictionary<string, string>());

        _validatorMock
            .Setup(x => x.Validate(It.IsAny<HttpTestResponse>(), It.Is<ExecutionTestCaseDto>(tc => tc.TestCaseId == testCase.TestCaseId), It.IsAny<ApiEndpointMetadataDto>()))
            .Returns(new TestCaseValidationResult
            {
                IsPassed = isPassed,
                StatusCodeMatched = true,
                Failures = isPassed
                    ? new List<ValidationFailureModel>()
                    : new List<ValidationFailureModel> { new() { Code = "STATUS_CODE_MISMATCH", Message = "Failed" } },
            });
    }

    private void SetupResultCollector()
    {
        _resultCollectorMock
            .Setup(x => x.CollectAsync(It.IsAny<TestRun>(), It.IsAny<IReadOnlyList<TestCaseExecutionResult>>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestRunResultModel
            {
                Run = new TestRunModel(),
                Cases = new List<TestCaseRunResultModel>(),
            });
    }

    private static ExecutionTestCaseDto CreateTestCase(
        Guid id,
        Guid endpointId,
        int orderIndex,
        Guid[]? dependencyIds = null)
    {
        return new ExecutionTestCaseDto
        {
            TestCaseId = id,
            EndpointId = endpointId,
            Name = $"Test Case {orderIndex}",
            TestType = "Functional",
            OrderIndex = orderIndex,
            DependencyIds = dependencyIds ?? Array.Empty<Guid>(),
            Request = new ExecutionTestCaseRequestDto
            {
                HttpMethod = "GET",
                Url = "/api/test",
                Timeout = 30000,
            },
            Expectation = new ExecutionTestCaseExpectationDto
            {
                ExpectedStatus = "[200]",
            },
            Variables = Array.Empty<ExecutionVariableRuleDto>(),
        };
    }

    #endregion
}
