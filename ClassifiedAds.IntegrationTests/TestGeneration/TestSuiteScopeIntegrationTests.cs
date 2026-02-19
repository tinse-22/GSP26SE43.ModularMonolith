using ClassifiedAds.Application;
using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.IntegrationTests.Infrastructure;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Persistence;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassifiedAds.IntegrationTests.TestGeneration;

[Collection("IntegrationTests")]
public class TestSuiteScopeIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainerFixture _dbFixture;
    private readonly Mock<IApiEndpointMetadataService> _endpointMetadataServiceMock = new();
    private CustomWebApplicationFactory _factory = null!;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid ApiSpecId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid Endpoint1 = Guid.NewGuid();
    private static readonly Guid Endpoint2 = Guid.NewGuid();
    private static readonly Guid Endpoint3 = Guid.NewGuid();

    public TestSuiteScopeIntegrationTests(PostgreSqlContainerFixture dbFixture)
    {
        _dbFixture = dbFixture;
    }

    public Task InitializeAsync()
    {
        // Setup default mock behavior: return valid endpoints for the spec
        _endpointMetadataServiceMock
            .Setup(x => x.GetEndpointMetadataAsync(
                ApiSpecId,
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid specId, IReadOnlyCollection<Guid> ids, CancellationToken _) =>
            {
                var validEndpoints = new HashSet<Guid> { Endpoint1, Endpoint2, Endpoint3 };
                return ids
                    .Where(id => validEndpoints.Contains(id))
                    .Select(id => new ApiEndpointMetadataDto
                    {
                        EndpointId = id,
                        HttpMethod = "GET",
                        Path = $"/api/test/{id}",
                    })
                    .ToList()
                    .AsReadOnly();
            });

        _factory = new CustomWebApplicationFactory(
            _dbFixture.ConnectionString,
            services =>
            {
                services.RemoveAll<IApiEndpointMetadataService>();
                services.AddSingleton(_endpointMetadataServiceMock.Object);
            });

        _ = _factory.Services;
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task CreateSuiteScope_WithValidEndpoints_ShouldPersistSelectedEndpointIds()
    {
        // Arrange & Act
        AddUpdateTestSuiteScopeCommand createCommand;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            createCommand = new AddUpdateTestSuiteScopeCommand
            {
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Integration Test Suite",
                Description = "Created by integration test",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint1, Endpoint2 },
            };

            await dispatcher.DispatchAsync(createCommand);
        }

        // Assert - verify persisted in DB
        createCommand.Result.Should().NotBeNull();
        createCommand.Result.Status.Should().Be(TestSuiteStatus.Draft);
        createCommand.Result.ApprovalStatus.Should().Be(ApprovalStatus.NotApplicable);
        createCommand.Result.SelectedEndpointIds.Should().HaveCount(2);
        createCommand.Result.SelectedEndpointCount.Should().Be(2);

        // Verify via query
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var fetched = await dispatcher.DispatchAsync(new GetTestSuiteScopeQuery
            {
                ProjectId = ProjectId,
                SuiteId = createCommand.Result.Id,
            });

            fetched.SelectedEndpointIds.Should().Contain(Endpoint1);
            fetched.SelectedEndpointIds.Should().Contain(Endpoint2);
            fetched.SelectedEndpointCount.Should().Be(2);
        }
    }

    [Fact]
    public async Task CreateSuiteScope_WithInvalidEndpoints_ShouldReturn400()
    {
        var invalidEndpoint = Guid.NewGuid();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();

        var command = new AddUpdateTestSuiteScopeCommand
        {
            ProjectId = ProjectId,
            CurrentUserId = UserId,
            Name = "Invalid Suite",
            ApiSpecId = ApiSpecId,
            GenerationType = GenerationType.Auto,
            SelectedEndpointIds = new List<Guid> { Endpoint1, invalidEndpoint },
        };

        var act = () => dispatcher.DispatchAsync(command);

        await act.Should().ThrowAsync<ValidationException>()
            .Where(ex => ex.Message.Contains(invalidEndpoint.ToString()));
    }

    [Fact]
    public async Task CreateSuiteScope_WithEmptyEndpoints_ShouldThrowValidation()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();

        var command = new AddUpdateTestSuiteScopeCommand
        {
            ProjectId = ProjectId,
            CurrentUserId = UserId,
            Name = "Empty Endpoints Suite",
            ApiSpecId = ApiSpecId,
            GenerationType = GenerationType.Auto,
            SelectedEndpointIds = new List<Guid>(),
        };

        var act = () => dispatcher.DispatchAsync(command);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateSuiteScope_AsNonOwner_ShouldThrowValidation()
    {
        // Arrange: create suite as UserId
        Guid suiteId;
        string rowVersion;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var createCommand = new AddUpdateTestSuiteScopeCommand
            {
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Owner Test Suite",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint1 },
            };
            await dispatcher.DispatchAsync(createCommand);
            suiteId = createCommand.Result.Id;
            rowVersion = createCommand.Result.RowVersion;
        }

        // Act: try to update as different user
        var nonOwner = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var updateCommand = new AddUpdateTestSuiteScopeCommand
            {
                SuiteId = suiteId,
                ProjectId = ProjectId,
                CurrentUserId = nonOwner,
                Name = "Updated by non-owner",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint1 },
                RowVersion = rowVersion,
            };

            var act = () => dispatcher.DispatchAsync(updateCommand);

            await act.Should().ThrowAsync<ValidationException>()
                .Where(ex => ex.Message.Contains("quyền"));
        }
    }

    [Fact]
    public async Task UpdateSuiteScope_WithStaleRowVersion_ShouldThrowConflict()
    {
        // Arrange: create suite
        Guid suiteId;
        string rowVersion;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var createCommand = new AddUpdateTestSuiteScopeCommand
            {
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Concurrency Test Suite",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint1 },
            };
            await dispatcher.DispatchAsync(createCommand);
            suiteId = createCommand.Result.Id;
            rowVersion = createCommand.Result.RowVersion;
        }

        // First update succeeds and changes rowVersion
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var updateCommand = new AddUpdateTestSuiteScopeCommand
            {
                SuiteId = suiteId,
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Updated Once",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint1, Endpoint2 },
                RowVersion = rowVersion,
            };
            await dispatcher.DispatchAsync(updateCommand);
        }

        // Second update with stale rowVersion should fail
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var staleCommand = new AddUpdateTestSuiteScopeCommand
            {
                SuiteId = suiteId,
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Should Fail",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint1 },
                RowVersion = rowVersion, // stale
            };

            var act = () => dispatcher.DispatchAsync(staleCommand);

            await act.Should().ThrowAsync<ConflictException>();
        }
    }

    [Fact]
    public async Task ArchiveSuiteScope_AsOwner_ShouldSetStatusArchived()
    {
        // Arrange
        Guid suiteId;
        string rowVersion;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var createCommand = new AddUpdateTestSuiteScopeCommand
            {
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Archive Test Suite",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint3 },
            };
            await dispatcher.DispatchAsync(createCommand);
            suiteId = createCommand.Result.Id;
            rowVersion = createCommand.Result.RowVersion;
        }

        // Act
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            await dispatcher.DispatchAsync(new ArchiveTestSuiteScopeCommand
            {
                SuiteId = suiteId,
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                RowVersion = rowVersion,
            });
        }

        // Assert
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestGenerationDbContext>();
            var suite = await db.TestSuites.FindAsync(suiteId);
            suite.Should().NotBeNull();
            suite!.Status.Should().Be(TestSuiteStatus.Archived);
        }
    }

    [Fact]
    public async Task UpdateArchivedSuite_ShouldThrowValidation()
    {
        // Arrange: create and archive
        Guid suiteId;
        string rowVersion;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var createCommand = new AddUpdateTestSuiteScopeCommand
            {
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "To Be Archived Suite",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint1 },
            };
            await dispatcher.DispatchAsync(createCommand);
            suiteId = createCommand.Result.Id;
            rowVersion = createCommand.Result.RowVersion;
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            await dispatcher.DispatchAsync(new ArchiveTestSuiteScopeCommand
            {
                SuiteId = suiteId,
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                RowVersion = rowVersion,
            });
        }

        // Get latest rowVersion after archive
        string latestRowVersion;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TestGenerationDbContext>();
            var suite = await db.TestSuites.FindAsync(suiteId);
            latestRowVersion = Convert.ToBase64String(suite!.RowVersion);
        }

        // Act: try to update archived suite
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var updateCommand = new AddUpdateTestSuiteScopeCommand
            {
                SuiteId = suiteId,
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Should Not Update",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint1 },
                RowVersion = latestRowVersion,
            };

            var act = () => dispatcher.DispatchAsync(updateCommand);

            await act.Should().ThrowAsync<ValidationException>()
                .Where(ex => ex.Message.Contains("archive"));
        }
    }

    [Fact]
    public async Task ListSuiteScopes_ShouldExcludeArchived()
    {
        // Arrange: unique project for this test
        var uniqueProjectId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();

            // Create active suite
            await dispatcher.DispatchAsync(new AddUpdateTestSuiteScopeCommand
            {
                ProjectId = uniqueProjectId,
                CurrentUserId = UserId,
                Name = "Active Suite",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint1 },
            });

            // Create and archive another suite
            var archiveCmd = new AddUpdateTestSuiteScopeCommand
            {
                ProjectId = uniqueProjectId,
                CurrentUserId = UserId,
                Name = "Archived Suite",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint2 },
            };
            await dispatcher.DispatchAsync(archiveCmd);

            await dispatcher.DispatchAsync(new ArchiveTestSuiteScopeCommand
            {
                SuiteId = archiveCmd.Result.Id,
                ProjectId = uniqueProjectId,
                CurrentUserId = UserId,
                RowVersion = archiveCmd.Result.RowVersion,
            });
        }

        // Act
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var result = await dispatcher.DispatchAsync(new GetTestSuiteScopesQuery
            {
                ProjectId = uniqueProjectId,
            });

            // Assert
            result.Should().HaveCount(1);
            result[0].Name.Should().Be("Active Suite");
        }
    }

    [Fact]
    public async Task ProposeOrder_WhenRequestEndpointsEmpty_ShouldFallbackToSuiteSelectedEndpoints()
    {
        Guid suiteId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            var createCommand = new AddUpdateTestSuiteScopeCommand
            {
                ProjectId = ProjectId,
                CurrentUserId = UserId,
                Name = "Fallback Scope Suite",
                ApiSpecId = ApiSpecId,
                GenerationType = GenerationType.Auto,
                SelectedEndpointIds = new List<Guid> { Endpoint1, Endpoint2 },
            };
            await dispatcher.DispatchAsync(createCommand);
            suiteId = createCommand.Result.Id;
        }

        ProposeApiTestOrderCommand proposeCommand;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<Dispatcher>();
            proposeCommand = new ProposeApiTestOrderCommand
            {
                TestSuiteId = suiteId,
                CurrentUserId = UserId,
                SpecificationId = ApiSpecId,
                SelectedEndpointIds = Array.Empty<Guid>(),
                Source = ProposalSource.Ai,
            };
            await dispatcher.DispatchAsync(proposeCommand);
        }

        proposeCommand.Result.Should().NotBeNull();
        proposeCommand.Result.ProposedOrder.Should().HaveCount(2);
        proposeCommand.Result.ProposedOrder.Select(x => x.EndpointId)
            .Should().BeEquivalentTo(new[] { Endpoint1, Endpoint2 });

        _endpointMetadataServiceMock.Verify(x => x.GetEndpointMetadataAsync(
            ApiSpecId,
            It.Is<IReadOnlyCollection<Guid>>(ids =>
                ids.Count == 2 &&
                ids.Contains(Endpoint1) &&
                ids.Contains(Endpoint2)),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }
}
