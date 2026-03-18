using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.IntegrationTests.Infrastructure;
using ClassifiedAds.Modules.TestExecution.Commands;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Persistence;
using ClassifiedAds.Modules.TestExecution.Queries;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.IntegrationTests.TestExecution;

[Collection("IntegrationTests")]
public class TestRunExecutionIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _dbFixture;
    private CustomWebApplicationFactory _factory = null!;

    private readonly Mock<ITestExecutionReadGatewayService> _gatewayMock = new();
    private readonly Mock<ISubscriptionLimitGatewayService> _limitServiceMock = new();
    private readonly Mock<IHttpTestExecutor> _httpExecutorMock = new();
    private readonly Mock<IApiEndpointMetadataService> _endpointMetadataMock = new();

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid SuiteId = Guid.NewGuid();
    private static readonly Guid ApiSpecId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid EnvironmentId = Guid.NewGuid();
    private static readonly Guid EndpointId1 = Guid.NewGuid();
    private static readonly Guid EndpointId2 = Guid.NewGuid();

    public TestRunExecutionIntegrationTests(PostgreSqlContainerFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public async Task InitializeAsync()
    {
        // Setup common mocks
        SetupDefaultGatewayMock();
        SetupDefaultLimitServiceMock();
        SetupDefaultEndpointMetadataMock();

        _factory = new CustomWebApplicationFactory(
            _dbFixture.ConnectionString,
            services =>
            {
                services.RemoveAll<ITestExecutionReadGatewayService>();
                services.AddSingleton(_gatewayMock.Object);

                services.RemoveAll<ISubscriptionLimitGatewayService>();
                services.AddSingleton(_limitServiceMock.Object);

                services.RemoveAll<IHttpTestExecutor>();
                services.AddSingleton(_httpExecutorMock.Object);

                services.RemoveAll<IApiEndpointMetadataService>();
                services.AddSingleton(_endpointMetadataMock.Object);
            });

        _ = _factory.Services;

        // Seed an execution environment
        await SeedEnvironment();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task StartTestRun_ShouldReturn201_AndSummaryCountersMatchDetailedResults()
    {
        // Arrange: 2 test cases, both pass
        var case1Id = Guid.NewGuid();
        var case2Id = Guid.NewGuid();

        SetupGatewayWithCases(new[]
        {
            CreateExecutionTestCase(case1Id, EndpointId1, 0),
            CreateExecutionTestCase(case2Id, EndpointId2, 1),
        });

        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ResolvedTestCaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 200,
                Body = "{\"success\":true}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 50,
            });

        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();

        // Act
        var command = new StartTestRunCommand
        {
            TestSuiteId = SuiteId,
            CurrentUserId = UserId,
            EnvironmentId = EnvironmentId,
        };
        await dispatcher.DispatchAsync(command);

        // Assert
        command.Result.Should().NotBeNull();
        command.Result.Run.Should().NotBeNull();
        command.Result.Cases.Should().HaveCount(2);

        var run = command.Result.Run;
        run.TotalTests.Should().Be(2);
        run.PassedCount.Should().Be(command.Result.Cases.Count(c => c.Status == "Passed"));
        run.FailedCount.Should().Be(command.Result.Cases.Count(c => c.Status == "Failed"));
        run.SkippedCount.Should().Be(command.Result.Cases.Count(c => c.Status == "Skipped"));
        (run.PassedCount + run.FailedCount + run.SkippedCount).Should().Be(run.TotalTests);
    }

    [Fact]
    public async Task StartTestRun_LoginTokenExtraction_ShouldFeedLaterProtectedRequest()
    {
        // Arrange: case1 = login (extracts token), case2 = uses token
        var loginCaseId = Guid.NewGuid();
        var protectedCaseId = Guid.NewGuid();

        var loginCase = CreateExecutionTestCase(loginCaseId, EndpointId1, 0);
        loginCase.Variables = new List<ExecutionVariableRuleDto>
        {
            new()
            {
                VariableName = "authToken",
                ExtractFrom = "ResponseBody",
                JsonPath = "$.token",
            },
        }.AsReadOnly();

        var protectedCase = CreateExecutionTestCase(protectedCaseId, EndpointId2, 1);
        protectedCase.Request.Headers = "{\"Authorization\":\"Bearer {{authToken}}\"}";

        SetupGatewayWithCases(new[] { loginCase, protectedCase });

        // Login returns token
        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.Is<ResolvedTestCaseRequest>(r => r.TestCaseId == loginCaseId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 200,
                Body = "{\"token\":\"jwt-extracted-token-123\"}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 100,
            });

        // Protected endpoint returns 200
        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(
                It.Is<ResolvedTestCaseRequest>(r => r.TestCaseId == protectedCaseId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 200,
                Body = "{\"data\":\"protected-content\"}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 50,
            });

        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();

        // Act
        var command = new StartTestRunCommand
        {
            TestSuiteId = SuiteId,
            CurrentUserId = UserId,
            EnvironmentId = EnvironmentId,
        };
        await dispatcher.DispatchAsync(command);

        // Assert
        command.Result.Should().NotBeNull();
        command.Result.Cases.Should().HaveCount(2);
        command.Result.Cases.Should().OnlyContain(c => c.Status == "Passed");
    }

    [Fact]
    public async Task StartTestRun_FailedDependency_ShouldSkipDependentCase()
    {
        // Arrange: case2 depends on case1, case1 fails
        var case1Id = Guid.NewGuid();
        var case2Id = Guid.NewGuid();

        var case1 = CreateExecutionTestCase(case1Id, EndpointId1, 0);
        case1.Expectation = new ExecutionTestCaseExpectationDto { ExpectedStatus = "[201]" }; // expects 201

        var case2 = CreateExecutionTestCase(case2Id, EndpointId2, 1);
        case2.DependencyIds = new[] { case1Id };

        SetupGatewayWithCases(new[] { case1, case2 });

        // Case1 returns 500 -> fails validation (expected 201)
        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ResolvedTestCaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 500,
                Body = "{\"error\":\"Internal Server Error\"}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 30,
            });

        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();

        // Act
        var command = new StartTestRunCommand
        {
            TestSuiteId = SuiteId,
            CurrentUserId = UserId,
            EnvironmentId = EnvironmentId,
        };
        await dispatcher.DispatchAsync(command);

        // Assert
        command.Result.Should().NotBeNull();
        command.Result.Cases.Should().HaveCount(2);

        var failedCase = command.Result.Cases.First(c => c.TestCaseId == case1Id);
        failedCase.Status.Should().Be("Failed");

        var skippedCase = command.Result.Cases.First(c => c.TestCaseId == case2Id);
        skippedCase.Status.Should().Be("Skipped");
        skippedCase.SkippedBecauseDependencyIds.Should().Contain(case1Id);
    }

    [Fact]
    public async Task GetTestRuns_ShouldBePaged_WithoutNeedingCache()
    {
        // Arrange: create multiple test runs
        for (int i = 0; i < 3; i++)
        {
            SetupGatewayWithCases(new[]
            {
                CreateExecutionTestCase(Guid.NewGuid(), EndpointId1, 0),
            });

            _httpExecutorMock
                .Setup(x => x.ExecuteAsync(It.IsAny<ResolvedTestCaseRequest>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new HttpTestResponse
                {
                    StatusCode = 200,
                    Body = "{}",
                    Headers = new Dictionary<string, string>(),
                    LatencyMs = 10,
                });

            await using var scope1 = _factory.Services.CreateAsyncScope();
            var dispatcher1 = scope1.ServiceProvider.GetRequiredService<Dispatcher>();
            await dispatcher1.DispatchAsync(new StartTestRunCommand
            {
                TestSuiteId = SuiteId,
                CurrentUserId = UserId,
                EnvironmentId = EnvironmentId,
            });
        }

        // Act: get paged results
        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();

        var page1 = await dispatcher.DispatchAsync(new GetTestRunsQuery
        {
            TestSuiteId = SuiteId,
            CurrentUserId = UserId,
            PageNumber = 1,
            PageSize = 2,
        });

        // Assert
        page1.Should().NotBeNull();
        page1.Items.Should().HaveCount(2);
        page1.TotalItems.Should().BeGreaterThanOrEqualTo(3);
        page1.Page.Should().Be(1);
        page1.PageSize.Should().Be(2);

        // Page 2
        var page2 = await dispatcher.DispatchAsync(new GetTestRunsQuery
        {
            TestSuiteId = SuiteId,
            CurrentUserId = UserId,
            PageNumber = 2,
            PageSize = 2,
        });

        page2.Items.Should().HaveCountGreaterThanOrEqualTo(1);
    }

    #region Helpers

    private async Task SeedEnvironment()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestExecutionDbContext>();

        var env = new ExecutionEnvironment
        {
            Id = EnvironmentId,
            ProjectId = ProjectId,
            Name = "Integration Test Env",
            BaseUrl = "https://api.test.local",
            IsDefault = true,
            Variables = "{\"env\":\"test\"}",
            Headers = "{}",
        };

        db.ExecutionEnvironments.Add(env);
        await db.SaveChangesAsync();
    }

    private void SetupDefaultGatewayMock()
    {
        _gatewayMock
            .Setup(x => x.GetSuiteAccessContextAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteAccessContextDto
            {
                TestSuiteId = SuiteId,
                ProjectId = ProjectId,
                ApiSpecId = ApiSpecId,
                CreatedById = UserId,
                Status = "Ready",
                Name = "Test Suite",
            });
    }

    private void SetupDefaultLimitServiceMock()
    {
        _limitServiceMock
            .Setup(x => x.CheckLimitAsync(It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true, LimitValue = 7 });

        _limitServiceMock
            .Setup(x => x.TryConsumeLimitAsync(It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });
    }

    private void SetupDefaultEndpointMetadataMock()
    {
        _endpointMetadataMock
            .Setup(x => x.GetEndpointMetadataAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid _, IReadOnlyCollection<Guid> ids, CancellationToken _) =>
                ids.Select(id => new ApiEndpointMetadataDto { EndpointId = id }).ToList().AsReadOnly());
    }

    private void SetupGatewayWithCases(ExecutionTestCaseDto[] cases)
    {
        _gatewayMock
            .Setup(x => x.GetExecutionContextAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TestSuiteExecutionContextDto
            {
                Suite = new TestSuiteAccessContextDto
                {
                    TestSuiteId = SuiteId,
                    ProjectId = ProjectId,
                    ApiSpecId = ApiSpecId,
                    Status = "Ready",
                },
                OrderedTestCases = cases,
                OrderedEndpointIds = cases.Where(c => c.EndpointId.HasValue).Select(c => c.EndpointId.Value).Distinct().ToList(),
            });
    }

    private static ExecutionTestCaseDto CreateExecutionTestCase(Guid id, Guid endpointId, int orderIndex)
    {
        return new ExecutionTestCaseDto
        {
            TestCaseId = id,
            EndpointId = endpointId,
            Name = $"IntegrationTestCase-{orderIndex}",
            TestType = "HappyPath",
            OrderIndex = orderIndex,
            DependencyIds = Array.Empty<Guid>(),
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
