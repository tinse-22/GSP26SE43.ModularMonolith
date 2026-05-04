using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Contracts.ApiDocumentation.Services;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class TestExecutionReadGatewayServiceTests
{
    private readonly Mock<IRepository<TestSuite, Guid>> _suiteRepoMock;
    private readonly Mock<IRepository<TestCase, Guid>> _testCaseRepoMock;
    private readonly Mock<IRepository<TestCaseRequest, Guid>> _requestRepoMock;
    private readonly Mock<IRepository<TestCaseExpectation, Guid>> _expectationRepoMock;
    private readonly Mock<IRepository<TestCaseVariable, Guid>> _variableRepoMock;
    private readonly Mock<IRepository<TestCaseDependency, Guid>> _dependencyRepoMock;
    private readonly Mock<IApiTestOrderGateService> _orderGateServiceMock;
    private readonly Mock<IProjectOwnershipGatewayService> _projectOwnershipGatewayServiceMock;
    private readonly TestExecutionReadGatewayService _service;

    private readonly Guid _suiteId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _apiSpecId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private const string ProjectName = "Checkout API";

    public TestExecutionReadGatewayServiceTests()
    {
        _suiteRepoMock = new Mock<IRepository<TestSuite, Guid>>();
        _testCaseRepoMock = new Mock<IRepository<TestCase, Guid>>();
        _requestRepoMock = new Mock<IRepository<TestCaseRequest, Guid>>();
        _expectationRepoMock = new Mock<IRepository<TestCaseExpectation, Guid>>();
        _variableRepoMock = new Mock<IRepository<TestCaseVariable, Guid>>();
        _dependencyRepoMock = new Mock<IRepository<TestCaseDependency, Guid>>();
        _orderGateServiceMock = new Mock<IApiTestOrderGateService>();
        _projectOwnershipGatewayServiceMock = new Mock<IProjectOwnershipGatewayService>();
        _projectOwnershipGatewayServiceMock
            .Setup(x => x.GetProjectNameAsync(_projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProjectName);

        _service = new TestExecutionReadGatewayService(
            _suiteRepoMock.Object,
            _testCaseRepoMock.Object,
            _requestRepoMock.Object,
            _expectationRepoMock.Object,
            _variableRepoMock.Object,
            _dependencyRepoMock.Object,
            _orderGateServiceMock.Object,
            _projectOwnershipGatewayServiceMock.Object);
    }

    [Fact]
    public async Task GetSuiteAccessContextAsync_SuiteNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite>().AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync((TestSuite)null);

        // Act
        var act = () => _service.GetSuiteAccessContextAsync(Guid.NewGuid());

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetSuiteAccessContextAsync_ValidSuite_ShouldReturnContext()
    {
        // Arrange
        var suite = CreateTestSuite();
        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite> { suite }.AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);

        // Act
        var result = await _service.GetSuiteAccessContextAsync(_suiteId);

        // Assert
        result.TestSuiteId.Should().Be(_suiteId);
        result.ProjectId.Should().Be(_projectId);
        result.ProjectName.Should().Be(ProjectName);
        result.CreatedById.Should().Be(_userId);
        result.Status.Should().Be("Ready");
    }

    [Fact]
    public async Task GetExecutionContextAsync_SelectedSubsetMissingDependency_ShouldAutoExpandToIncludeDependency()
    {
        // Arrange: caseB depends on caseA, but only caseB is selected.
        // Auto-expansion (BFS) should silently include caseA so the run succeeds.
        var caseAId = Guid.NewGuid();
        var caseBId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var caseA = CreateTestCase(caseAId, endpointId, 0);
        var caseB = CreateTestCase(caseBId, endpointId, 1);

        var dependency = new TestCaseDependency
        {
            Id = Guid.NewGuid(),
            TestCaseId = caseBId,
            DependsOnTestCaseId = caseAId,
        };

        SetupGatewayMocks(new[] { caseA, caseB }, new[] { dependency }, endpointId);

        // Act — select only caseB; caseA is auto-expanded as a transitive dependency
        var result = await _service.GetExecutionContextAsync(_suiteId, new[] { caseBId });

        // Assert — both cases are present; caseA (dependency) runs before caseB
        result.OrderedTestCases.Should().HaveCount(2);
        result.OrderedTestCases.Select(x => x.TestCaseId)
            .Should().ContainInOrder(caseAId, caseBId);
    }

    [Fact]
    public async Task GetExecutionContextAsync_SelectedSubsetWithAllDependencies_ShouldSucceed()
    {
        // Arrange: caseB depends on caseA, both selected
        var caseAId = Guid.NewGuid();
        var caseBId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var caseA = CreateTestCase(caseAId, endpointId, 0);
        var caseB = CreateTestCase(caseBId, endpointId, 1);

        var dependency = new TestCaseDependency
        {
            Id = Guid.NewGuid(),
            TestCaseId = caseBId,
            DependsOnTestCaseId = caseAId,
        };

        SetupGatewayMocks(new[] { caseA, caseB }, new[] { dependency }, endpointId);

        // Act — select both
        var result = await _service.GetExecutionContextAsync(_suiteId, new[] { caseAId, caseBId });

        // Assert
        result.OrderedTestCases.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetExecutionContextAsync_Should_OrderCasesTopologicallyByDependency()
    {
        // Arrange: baseline order C -> B -> A, dependencies require A -> B -> C
        var endpointId = Guid.NewGuid();
        var caseAId = Guid.NewGuid();
        var caseBId = Guid.NewGuid();
        var caseCId = Guid.NewGuid();

        var caseA = CreateTestCase(caseAId, endpointId, 2);
        var caseB = CreateTestCase(caseBId, endpointId, 1);
        var caseC = CreateTestCase(caseCId, endpointId, 0);

        var deps = new[]
        {
            new TestCaseDependency { Id = Guid.NewGuid(), TestCaseId = caseBId, DependsOnTestCaseId = caseAId },
            new TestCaseDependency { Id = Guid.NewGuid(), TestCaseId = caseCId, DependsOnTestCaseId = caseBId },
        };

        SetupGatewayMocks(new[] { caseA, caseB, caseC }, deps, endpointId);

        // Act
        var result = await _service.GetExecutionContextAsync(_suiteId, null);

        // Assert
        result.OrderedTestCases.Select(x => x.TestCaseId)
            .Should().ContainInOrder(caseAId, caseBId, caseCId);

        result.OrderedTestCases.Select(x => x.OrderIndex)
            .Should().ContainInOrder(0, 1, 2);
    }

    [Fact]
    public async Task GetExecutionContextAsync_Should_Prioritize_CustomOrder_WhenAnyCustomized()
    {
        // Arrange: custom order conflicts with approved endpoint order
        var endpointA = Guid.NewGuid();
        var endpointB = Guid.NewGuid();
        var caseAId = Guid.NewGuid();
        var caseBId = Guid.NewGuid();

        var caseA = CreateTestCase(caseAId, endpointA, 0);
        var caseB = CreateTestCase(caseBId, endpointB, 1);

        caseA.IsOrderCustomized = true;
        caseA.CustomOrderIndex = 1;
        caseB.IsOrderCustomized = true;
        caseB.CustomOrderIndex = 0;

        SetupGatewayMocks(new[] { caseA, caseB }, Array.Empty<TestCaseDependency>(), endpointA, endpointB);

        // Act
        var result = await _service.GetExecutionContextAsync(_suiteId, null);

        // Assert
        result.OrderedTestCases.Select(x => x.TestCaseId)
            .Should().ContainInOrder(caseBId, caseAId);
    }

    [Fact]
    public async Task GetExecutionContextAsync_WhenDependencyCycleExists_ShouldBreakCycleAndOrderAll()
    {
        // Arrange: A depends on B, B depends on A (cycle)
        var endpointId = Guid.NewGuid();
        var caseAId = Guid.NewGuid();
        var caseBId = Guid.NewGuid();

        var caseA = CreateTestCase(caseAId, endpointId, 0);
        var caseB = CreateTestCase(caseBId, endpointId, 1);

        var deps = new[]
        {
            new TestCaseDependency { Id = Guid.NewGuid(), TestCaseId = caseAId, DependsOnTestCaseId = caseBId },
            new TestCaseDependency { Id = Guid.NewGuid(), TestCaseId = caseBId, DependsOnTestCaseId = caseAId },
        };

        SetupGatewayMocks(new[] { caseA, caseB }, deps, endpointId);

        // Act — should not throw; cycles are broken gracefully
        var result = await _service.GetExecutionContextAsync(_suiteId, null);

        // Assert — all test cases are returned; cycle is broken by baseline order
        result.OrderedTestCases.Should().HaveCount(2);
        result.OrderedTestCases.Select(x => x.TestCaseId)
            .Should().Contain(caseAId)
            .And.Contain(caseBId);
    }

    [Fact]
    public async Task GetExecutionContextAsync_SelectDisabledTestCase_ShouldThrowValidation()
    {
        // Arrange
        var caseId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var disabledCase = CreateTestCase(caseId, endpointId, 0);
        disabledCase.IsEnabled = false;

        SetupGatewayMocks(new[] { disabledCase }, Array.Empty<TestCaseDependency>(), endpointId);

        // Override testcase repo to return the disabled case
        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(new List<TestCase>()); // enabled list is empty

        // Act
        var act = () => _service.GetExecutionContextAsync(_suiteId, new[] { caseId });

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetExecutionContextAsync_NullSelectedIds_ShouldRunAllEnabled()
    {
        // Arrange
        var case1Id = Guid.NewGuid();
        var case2Id = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var case1 = CreateTestCase(case1Id, endpointId, 0);
        var case2 = CreateTestCase(case2Id, endpointId, 1);

        SetupGatewayMocks(new[] { case1, case2 }, Array.Empty<TestCaseDependency>(), endpointId);

        // Act — null/empty selectedIds means run all
        var result = await _service.GetExecutionContextAsync(_suiteId, null);

        // Assert
        result.OrderedTestCases.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetExecutionContextAsync_TransitiveDependencyMissing_ShouldAutoExpandTransitively()
    {
        // Arrange: C depends on B, B depends on A. Select only B and C (missing A).
        // Auto-expansion BFS should include A via B's dependency chain.
        var caseAId = Guid.NewGuid();
        var caseBId = Guid.NewGuid();
        var caseCId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();

        var caseA = CreateTestCase(caseAId, endpointId, 0);
        var caseB = CreateTestCase(caseBId, endpointId, 1);
        var caseC = CreateTestCase(caseCId, endpointId, 2);

        var deps = new[]
        {
            new TestCaseDependency { Id = Guid.NewGuid(), TestCaseId = caseBId, DependsOnTestCaseId = caseAId },
            new TestCaseDependency { Id = Guid.NewGuid(), TestCaseId = caseCId, DependsOnTestCaseId = caseBId },
        };

        SetupGatewayMocks(new[] { caseA, caseB, caseC }, deps, endpointId);

        // Act — select B and C; A is auto-expanded as a transitive dependency of B
        var result = await _service.GetExecutionContextAsync(_suiteId, new[] { caseBId, caseCId });

        // Assert — all 3 cases are present in correct topological order A→B→C
        result.OrderedTestCases.Should().HaveCount(3);
        result.OrderedTestCases.Select(x => x.TestCaseId)
            .Should().ContainInOrder(caseAId, caseBId, caseCId);
    }

    [Fact]
    public async Task GetExecutionContextAsync_BatchLoadsDataWithoutNPlusOne()
    {
        // Arrange: multiple cases - verify repositories are called once each (batch load)
        var cases = Enumerable.Range(0, 5)
            .Select(i => CreateTestCase(Guid.NewGuid(), Guid.NewGuid(), i))
            .ToArray();

        var endpointIds = cases.Select(c => c.EndpointId.Value).ToArray();

        SetupGatewayMocks(cases, Array.Empty<TestCaseDependency>(), endpointIds);

        // Act
        await _service.GetExecutionContextAsync(_suiteId, null);

        // Assert — each repository ToListAsync called exactly once (batch, no N+1)
        _requestRepoMock.Verify(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseRequest>>()), Times.Once);
        _expectationRepoMock.Verify(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseExpectation>>()), Times.Once);
        _variableRepoMock.Verify(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseVariable>>()), Times.Once);
        _dependencyRepoMock.Verify(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseDependency>>()), Times.Once);
    }

    [Fact]
    public async Task GetExecutionContextAsync_Should_MapVariableRegex_ForExecution()
    {
        var caseId = Guid.NewGuid();
        var endpointId = Guid.NewGuid();
        var testCase = CreateTestCase(caseId, endpointId, 0);
        var variable = new TestCaseVariable
        {
            Id = Guid.NewGuid(),
            TestCaseId = caseId,
            VariableName = "authToken",
            ExtractFrom = ExtractFrom.ResponseHeader,
            HeaderName = "Authorization",
            Regex = "(?:Bearer\\s+)?(?<value>[^\\s]+)$",
        };

        SetupGatewayMocks(new[] { testCase }, Array.Empty<TestCaseDependency>(), endpointId);
        _variableRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseVariable>>()))
            .ReturnsAsync(new List<TestCaseVariable> { variable });

        var result = await _service.GetExecutionContextAsync(_suiteId, null);

        result.OrderedTestCases.Should().ContainSingle();
        result.OrderedTestCases[0].Variables.Should().ContainSingle();
        result.OrderedTestCases[0].Variables[0].Regex.Should().Be("(?:Bearer\\s+)?(?<value>[^\\s]+)$");
    }

    #region Helpers

    private TestSuite CreateTestSuite()
    {
        return new TestSuite
        {
            Id = _suiteId,
            ProjectId = _projectId,
            ApiSpecId = _apiSpecId,
            Name = "Test Suite",
            CreatedById = _userId,
            Status = TestSuiteStatus.Ready,
        };
    }

    private TestCase CreateTestCase(Guid id, Guid endpointId, int orderIndex)
    {
        return new TestCase
        {
            Id = id,
            TestSuiteId = _suiteId,
            EndpointId = endpointId,
            Name = $"TestCase-{orderIndex}",
            TestType = TestType.HappyPath,
            IsEnabled = true,
            OrderIndex = orderIndex,
        };
    }

    private void SetupGatewayMocks(TestCase[] cases, TestCaseDependency[] dependencies, params Guid[] endpointIds)
    {
        // Suite
        var suite = CreateTestSuite();
        _suiteRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestSuite> { suite }.AsQueryable());
        _suiteRepoMock.Setup(x => x.FirstOrDefaultAsync(It.IsAny<IQueryable<TestSuite>>()))
            .ReturnsAsync(suite);

        // Enabled test cases
        _testCaseRepoMock.Setup(x => x.GetQueryableSet()).Returns(cases.AsQueryable());
        _testCaseRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCase>>()))
            .ReturnsAsync(cases.Where(c => c.IsEnabled).ToList());

        // Requests (empty)
        _requestRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestCaseRequest>().AsQueryable());
        _requestRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseRequest>>()))
            .ReturnsAsync(new List<TestCaseRequest>());

        // Expectations (empty)
        _expectationRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestCaseExpectation>().AsQueryable());
        _expectationRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseExpectation>>()))
            .ReturnsAsync(new List<TestCaseExpectation>());

        // Variables (empty)
        _variableRepoMock.Setup(x => x.GetQueryableSet()).Returns(new List<TestCaseVariable>().AsQueryable());
        _variableRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseVariable>>()))
            .ReturnsAsync(new List<TestCaseVariable>());

        // Dependencies
        _dependencyRepoMock.Setup(x => x.GetQueryableSet()).Returns(dependencies.AsQueryable());
        _dependencyRepoMock.Setup(x => x.ToListAsync(It.IsAny<IQueryable<TestCaseDependency>>()))
            .ReturnsAsync(dependencies.ToList());

        // Order gate
        _orderGateServiceMock
            .Setup(x => x.RequireApprovedOrderAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(endpointIds.Select((id, idx) => new ApiOrderItemModel
            {
                EndpointId = id,
                OrderIndex = idx,
            }).ToList().AsReadOnly());
    }

    #endregion
}
