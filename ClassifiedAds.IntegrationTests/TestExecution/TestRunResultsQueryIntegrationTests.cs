using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.Contracts.Subscription.DTOs;
using ClassifiedAds.Contracts.Subscription.Enums;
using ClassifiedAds.Contracts.Subscription.Services;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.TestGeneration.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.IntegrationTests.Infrastructure;
using ClassifiedAds.Modules.TestExecution.Commands;
using ClassifiedAds.Modules.TestExecution.Entities;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Persistence;
using ClassifiedAds.Modules.TestExecution.Queries;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.IntegrationTests.TestExecution;

[Collection("IntegrationTests")]
public class TestRunResultsQueryIntegrationTests : IAsyncLifetime
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
    private static readonly Guid EndpointId = Guid.NewGuid();

    public TestRunResultsQueryIntegrationTests(PostgreSqlContainerFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public async Task InitializeAsync()
    {
        SetupDefaultMocks();

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
        await SeedEnvironment();
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task GetTestRunResults_AfterExpiry_ShouldThrowConflictWithExpiredCode()
    {
        // Arrange: create a run, then manually expire it
        var caseId = Guid.NewGuid();
        SetupGatewayWithCases(new[] { CreateTestCase(caseId, EndpointId, 0) });

        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ResolvedTestCaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 200,
                Body = "{}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 10,
            });

        Guid runId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var command = new StartTestRunCommand
            {
                TestSuiteId = SuiteId,
                CurrentUserId = UserId,
                EnvironmentId = EnvironmentId,
            };
            await dispatcher.DispatchAsync(command);
            runId = command.Result.Run.Id;
        }

        // Manually expire the run
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestExecutionDbContext>();
            var run = await db.TestRuns.FirstOrDefaultAsync(r => r.Id == runId);
            run.ResultsExpireAt = DateTimeOffset.UtcNow.AddDays(-1); // expired
            await db.SaveChangesAsync();
        }

        // Act
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var act = () => dispatcher.DispatchAsync(new GetTestRunResultsQuery
            {
                TestSuiteId = SuiteId,
                RunId = runId,
                CurrentUserId = UserId,
            });

            // Assert
            var ex = await act.Should().ThrowAsync<ConflictException>();
            ex.Which.ReasonCode.Should().Be("RUN_RESULTS_EXPIRED");
        }
    }

    [Fact]
    public async Task GetTestRunResults_WithinExpiry_ShouldReturnCachedResults()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        SetupGatewayWithCases(new[] { CreateTestCase(caseId, EndpointId, 0) });

        _httpExecutorMock
            .Setup(x => x.ExecuteAsync(It.IsAny<ResolvedTestCaseRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HttpTestResponse
            {
                StatusCode = 200,
                Body = "{\"result\":\"ok\"}",
                Headers = new Dictionary<string, string>(),
                LatencyMs = 25,
            });

        Guid runId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var command = new StartTestRunCommand
            {
                TestSuiteId = SuiteId,
                CurrentUserId = UserId,
                EnvironmentId = EnvironmentId,
            };
            await dispatcher.DispatchAsync(command);
            runId = command.Result.Run.Id;
        }

        // Act
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var result = await dispatcher.DispatchAsync(new GetTestRunResultsQuery
            {
                TestSuiteId = SuiteId,
                RunId = runId,
                CurrentUserId = UserId,
            });

            // Assert
            result.Should().NotBeNull();
            result.Cases.Should().HaveCount(1);
            result.Run.Id.Should().Be(runId);
        }
    }

    #region Helpers

    private async Task SeedEnvironment()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestExecutionDbContext>();

        if (!await db.ExecutionEnvironments.AnyAsync(e => e.Id == EnvironmentId))
        {
            db.ExecutionEnvironments.Add(new ExecutionEnvironment
            {
                Id = EnvironmentId,
                ProjectId = ProjectId,
                Name = "Results Query Test Env",
                BaseUrl = "https://api.results.local",
                IsDefault = true,
                Variables = "{\"env\":\"test\"}",
                Headers = "{}",
            });
            await db.SaveChangesAsync();
        }
    }

    private void SetupDefaultMocks()
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

        _limitServiceMock
            .Setup(x => x.CheckLimitAsync(It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true, LimitValue = 7 });

        _limitServiceMock
            .Setup(x => x.TryConsumeLimitAsync(It.IsAny<Guid>(), It.IsAny<LimitType>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LimitCheckResultDTO { IsAllowed = true });

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

    private static ExecutionTestCaseDto CreateTestCase(Guid id, Guid endpointId, int orderIndex)
    {
        return new ExecutionTestCaseDto
        {
            TestCaseId = id,
            EndpointId = endpointId,
            Name = $"ResultsTestCase-{orderIndex}",
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
