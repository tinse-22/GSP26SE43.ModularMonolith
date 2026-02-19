using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.IntegrationTests.Infrastructure;
using ClassifiedAds.Modules.TestExecution.Commands;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Persistence;
using ClassifiedAds.Modules.TestExecution.Queries;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassifiedAds.IntegrationTests.TestExecution;

[Collection("IntegrationTests")]
public class ExecutionEnvironmentIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _dbFixture;
    private CustomWebApplicationFactory _factory = null!;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    public ExecutionEnvironmentIntegrationTests(PostgreSqlContainerFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public Task InitializeAsync()
    {
        _factory = new CustomWebApplicationFactory(_dbFixture.ConnectionString);
        _ = _factory.Services;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateEnvironment_WithValidData_ShouldPersist()
    {
        AddUpdateExecutionEnvironmentCommand createCommand;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            createCommand = new AddUpdateExecutionEnvironmentCommand
            {
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Development",
                BaseUrl = "https://dev.example.com",
                Variables = new Dictionary<string, string> { { "ENV", "dev" } },
                Headers = new Dictionary<string, string> { { "X-Custom", "test" } },
                AuthConfig = new ExecutionAuthConfigModel
                {
                    AuthType = AuthType.BearerToken,
                    Token = "secret-token-12345",
                },
                IsDefault = false,
            };

            await dispatcher.DispatchAsync(createCommand);
        }

        // Assert
        createCommand.Result.Should().NotBeNull();
        createCommand.Result.Name.Should().Be("Development");
        createCommand.Result.BaseUrl.Should().Be("https://dev.example.com");
        createCommand.Result.IsDefault.Should().BeFalse();

        // Verify via query
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var fetched = await dispatcher.DispatchAsync(new GetExecutionEnvironmentQuery
            {
                ProjectId = ProjectId,
                EnvironmentId = createCommand.Result.Id,
            });

            fetched.Name.Should().Be("Development");
            fetched.Variables.Should().ContainKey("ENV");
        }
    }

    [Fact]
    public async Task CreateEnvironment_WithInvalidBaseUrl_ShouldThrowValidation()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();

        var command = new AddUpdateExecutionEnvironmentCommand
        {
            ProjectId = ProjectId,
            CurrentUserId = UserId,
            Name = "Bad URL Env",
            BaseUrl = "not-a-url",
            IsDefault = false,
        };

        var act = () => dispatcher.DispatchAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Message.Contains("BaseUrl"));
    }

    [Fact]
    public async Task CreateEnvironment_WithEmptyName_ShouldThrowValidation()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();

        var command = new AddUpdateExecutionEnvironmentCommand
        {
            ProjectId = ProjectId,
            CurrentUserId = UserId,
            Name = "",
            BaseUrl = "https://dev.example.com",
            IsDefault = false,
        };

        var act = () => dispatcher.DispatchAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task SetDefaultEnvironment_ShouldUnsetPreviousDefault()
    {
        var uniqueProjectId = Guid.NewGuid();

        // Create first default environment
        Guid firstEnvId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var cmd1 = new AddUpdateExecutionEnvironmentCommand
            {
                ProjectId = uniqueProjectId,
                CurrentUserId = UserId,
                Name = "Env A",
                BaseUrl = "https://env-a.example.com",
                IsDefault = true,
            };
            await dispatcher.DispatchAsync(cmd1);
            firstEnvId = cmd1.Result.Id;
        }

        // Create second default environment
        Guid secondEnvId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var cmd2 = new AddUpdateExecutionEnvironmentCommand
            {
                ProjectId = uniqueProjectId,
                CurrentUserId = UserId,
                Name = "Env B",
                BaseUrl = "https://env-b.example.com",
                IsDefault = true,
            };
            await dispatcher.DispatchAsync(cmd2);
            secondEnvId = cmd2.Result.Id;
        }

        // Assert: only Env B is default
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestExecutionDbContext>();
            var envA = await db.ExecutionEnvironments.FindAsync(firstEnvId);
            var envB = await db.ExecutionEnvironments.FindAsync(secondEnvId);

            envA!.IsDefault.Should().BeFalse("previous default should be unset");
            envB!.IsDefault.Should().BeTrue("new default should be set");

            // Only one default per project
            var defaultCount = await db.ExecutionEnvironments
                .CountAsync(e => e.ProjectId == uniqueProjectId && e.IsDefault);
            defaultCount.Should().Be(1);
        }
    }

    [Fact]
    public async Task UpdateEnvironment_WithStaleRowVersion_ShouldThrowConflict()
    {
        // Arrange: create environment
        Guid envId;
        string rowVersion;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var createCmd = new AddUpdateExecutionEnvironmentCommand
            {
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Concurrency Env",
                BaseUrl = "https://concurrency.example.com",
                IsDefault = false,
            };
            await dispatcher.DispatchAsync(createCmd);
            envId = createCmd.Result.Id;
            rowVersion = createCmd.Result.RowVersion;
        }

        // First update succeeds
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            await dispatcher.DispatchAsync(new AddUpdateExecutionEnvironmentCommand
            {
                EnvironmentId = envId,
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Updated Concurrency Env",
                BaseUrl = "https://concurrency.example.com",
                IsDefault = false,
                RowVersion = rowVersion,
            });
        }

        // Second update with stale rowVersion should fail
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var staleCmd = new AddUpdateExecutionEnvironmentCommand
            {
                EnvironmentId = envId,
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Should Fail",
                BaseUrl = "https://concurrency.example.com",
                IsDefault = false,
                RowVersion = rowVersion, // stale
            };

            var act = () => dispatcher.DispatchAsync(staleCmd);

            await act.Should().ThrowAsync<ConflictException>();
        }
    }

    [Fact]
    public async Task DeleteEnvironment_ShouldRemoveFromDb()
    {
        var uniqueProjectId = Guid.NewGuid();

        // Create
        Guid envId;
        string rowVersion;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var createCmd = new AddUpdateExecutionEnvironmentCommand
            {
                ProjectId = uniqueProjectId,
                CurrentUserId = UserId,
                Name = "To Delete",
                BaseUrl = "https://delete.example.com",
                IsDefault = false,
            };
            await dispatcher.DispatchAsync(createCmd);
            envId = createCmd.Result.Id;
            rowVersion = createCmd.Result.RowVersion;
        }

        // Delete
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            await dispatcher.DispatchAsync(new DeleteExecutionEnvironmentCommand
            {
                EnvironmentId = envId,
                ProjectId = uniqueProjectId,
                CurrentUserId = UserId,
                RowVersion = rowVersion,
            });
        }

        // Assert
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestExecutionDbContext>();
            var env = await db.ExecutionEnvironments.FindAsync(envId);
            env.Should().BeNull("environment should be hard-deleted");
        }
    }

    [Fact]
    public async Task GetEnvironmentResponse_ShouldMaskAuthSecrets()
    {
        var uniqueProjectId = Guid.NewGuid();

        // Create with real secrets
        Guid envId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var createCmd = new AddUpdateExecutionEnvironmentCommand
            {
                ProjectId = uniqueProjectId,
                CurrentUserId = UserId,
                Name = "Secret Env",
                BaseUrl = "https://secret.example.com",
                AuthConfig = new ExecutionAuthConfigModel
                {
                    AuthType = AuthType.OAuth2ClientCredentials,
                    TokenUrl = "https://auth.example.com/token",
                    ClientId = "my-client-id",
                    ClientSecret = "super-secret-value",
                    Scopes = new[] { "read", "write" },
                },
                IsDefault = false,
            };
            await dispatcher.DispatchAsync(createCmd);
            envId = createCmd.Result.Id;

            // Assert: response should have masked secret
            createCmd.Result.AuthConfig.Should().NotBeNull();
            createCmd.Result.AuthConfig.ClientSecret.Should().Be("******");
            createCmd.Result.AuthConfig.ClientId.Should().Be("my-client-id"); // non-secret should not be masked
        }

        // Verify via query as well
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var fetched = await dispatcher.DispatchAsync(new GetExecutionEnvironmentQuery
            {
                ProjectId = uniqueProjectId,
                EnvironmentId = envId,
            });

            fetched.AuthConfig.ClientSecret.Should().Be("******");
            fetched.AuthConfig.TokenUrl.Should().Be("https://auth.example.com/token");
        }
    }

    [Fact]
    public async Task ListEnvironments_ShouldReturnAllForProject()
    {
        var uniqueProjectId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();

            await dispatcher.DispatchAsync(new AddUpdateExecutionEnvironmentCommand
            {
                ProjectId = uniqueProjectId,
                CurrentUserId = UserId,
                Name = "Env 1",
                BaseUrl = "https://env1.example.com",
                IsDefault = false,
            });

            await dispatcher.DispatchAsync(new AddUpdateExecutionEnvironmentCommand
            {
                ProjectId = uniqueProjectId,
                CurrentUserId = UserId,
                Name = "Env 2",
                BaseUrl = "https://env2.example.com",
                IsDefault = true,
            });
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var result = await dispatcher.DispatchAsync(new GetExecutionEnvironmentsQuery
            {
                ProjectId = uniqueProjectId,
            });

            result.Should().HaveCount(2);
            result.Count(e => e.IsDefault).Should().Be(1);
        }
    }
}
