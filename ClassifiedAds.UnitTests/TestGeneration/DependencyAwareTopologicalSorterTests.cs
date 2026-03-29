using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for DependencyAwareTopologicalSorter.
/// Covers: null/empty inputs, dependency ordering, cycle detection/breaking,
/// fan-out ranking, auth-first priority, and deterministic tie-breaking.
/// Source: KAT paper (arXiv:2407.10227) Section 4.3 - Sequence Generation.
/// </summary>
public class DependencyAwareTopologicalSorterTests
{
    private readonly DependencyAwareTopologicalSorter _sut;

    public DependencyAwareTopologicalSorterTests()
    {
        _sut = new DependencyAwareTopologicalSorter();
    }

    #region Null/Empty Input Tests

    [Fact]
    public void Sort_Should_ReturnEmpty_WhenOperationsIsNull()
    {
        // Act
        var result = _sut.Sort(null, Array.Empty<DependencyEdge>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Sort_Should_ReturnEmpty_WhenOperationsIsEmpty()
    {
        // Act
        var result = _sut.Sort(Array.Empty<SortableOperation>(), Array.Empty<DependencyEdge>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Sort_Should_HandleNullEdges()
    {
        // Arrange
        var op = CreateOperation("GET", "/api/users");
        var operations = new[] { op };

        // Act
        var result = _sut.Sort(operations, null);

        // Assert
        result.Should().HaveCount(1);
        result[0].OperationId.Should().Be(op.OperationId);
    }

    #endregion

    #region Single Operation Tests

    [Fact]
    public void Sort_Should_ReturnSingleOperation_WhenOnlyOneExists()
    {
        // Arrange
        var op = CreateOperation("GET", "/api/users");
        var operations = new[] { op };

        // Act
        var result = _sut.Sort(operations, Array.Empty<DependencyEdge>());

        // Assert
        result.Should().HaveCount(1);
        result[0].OperationId.Should().Be(op.OperationId);
        result[0].OrderIndex.Should().Be(1);
        result[0].FanOut.Should().Be(0);
        result[0].IsCycleBreak.Should().BeFalse();
        result[0].ReasonCodes.Should().Contain("DETERMINISTIC_TIE_BREAK");
    }

    #endregion

    #region Dependency Ordering Tests

    [Fact]
    public void Sort_Should_OrderDependencyFirst_WhenSimpleDependencyExists()
    {
        // Arrange
        var postUsers = CreateOperation("POST", "/api/users");
        var getUserById = CreateOperation("GET", "/api/users/{id}");

        var operations = new[] { getUserById, postUsers };
        var edges = new[]
        {
            new DependencyEdge
            {
                SourceOperationId = getUserById.OperationId,
                TargetOperationId = postUsers.OperationId,
                Type = DependencyEdgeType.PathBased,
                Confidence = 1.0,
            },
        };

        // Act
        var result = _sut.Sort(operations, edges);

        // Assert
        result.Should().HaveCount(2);
        result[0].OperationId.Should().Be(postUsers.OperationId);
        result[1].OperationId.Should().Be(getUserById.OperationId);
        result[1].Dependencies.Should().Contain(postUsers.OperationId);
        result[1].ReasonCodes.Should().Contain("DEPENDENCY_FIRST");
    }

    [Fact]
    public void Sort_Should_HandleTransitiveDependencies()
    {
        // Arrange: A depends on B, B depends on C → order should be C, B, A
        var opA = CreateOperation("GET", "/api/orders/{id}");
        var opB = CreateOperation("POST", "/api/orders");
        var opC = CreateOperation("POST", "/api/users");

        var operations = new[] { opA, opB, opC };
        var edges = new[]
        {
            new DependencyEdge
            {
                SourceOperationId = opA.OperationId,
                TargetOperationId = opB.OperationId,
                Type = DependencyEdgeType.PathBased,
                Confidence = 1.0,
            },
            new DependencyEdge
            {
                SourceOperationId = opB.OperationId,
                TargetOperationId = opC.OperationId,
                Type = DependencyEdgeType.OperationSchema,
                Confidence = 1.0,
            },
        };

        // Act
        var result = _sut.Sort(operations, edges);

        // Assert
        var orderMap = result.ToDictionary(r => r.OperationId, r => r.OrderIndex);
        orderMap[opC.OperationId].Should().BeLessThan(orderMap[opB.OperationId]);
        orderMap[opB.OperationId].Should().BeLessThan(orderMap[opA.OperationId]);
    }

    [Fact]
    public void Sort_Should_IgnoreLowConfidenceEdges()
    {
        // Arrange
        var postUsers = CreateOperation("POST", "/api/users");
        var getUserById = CreateOperation("GET", "/api/users/{id}");

        var operations = new[] { getUserById, postUsers };
        var edges = new[]
        {
            new DependencyEdge
            {
                SourceOperationId = getUserById.OperationId,
                TargetOperationId = postUsers.OperationId,
                Type = DependencyEdgeType.SemanticToken,
                Confidence = 0.4, // Below threshold (0.5)
            },
        };

        // Act
        var result = _sut.Sort(operations, edges);

        // Assert: should use deterministic ordering since edge is ignored
        result.Should().HaveCount(2);
        // POST before GET in deterministic ordering
        result[0].OperationId.Should().Be(postUsers.OperationId);
        result[1].OperationId.Should().Be(getUserById.OperationId);
        result[1].Dependencies.Should().BeEmpty(); // Edge was ignored
    }

    [Fact]
    public void Sort_Should_IgnoreSelfDependencies()
    {
        // Arrange
        var op = CreateOperation("GET", "/api/users");

        var operations = new[] { op };
        var edges = new[]
        {
            new DependencyEdge
            {
                SourceOperationId = op.OperationId,
                TargetOperationId = op.OperationId, // Self-reference
                Type = DependencyEdgeType.PathBased,
                Confidence = 1.0,
            },
        };

        // Act
        var result = _sut.Sort(operations, edges);

        // Assert
        result.Should().HaveCount(1);
        result[0].Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Sort_Should_IgnoreEdgesToUnknownOperations()
    {
        // Arrange
        var op = CreateOperation("GET", "/api/users");
        var unknownId = Guid.NewGuid();

        var operations = new[] { op };
        var edges = new[]
        {
            new DependencyEdge
            {
                SourceOperationId = op.OperationId,
                TargetOperationId = unknownId, // Not in our operation set
                Type = DependencyEdgeType.PathBased,
                Confidence = 1.0,
            },
        };

        // Act
        var result = _sut.Sort(operations, edges);

        // Assert
        result.Should().HaveCount(1);
        result[0].Dependencies.Should().BeEmpty();
    }

    #endregion

    #region Cycle Detection and Breaking Tests

    [Fact]
    public void Sort_Should_BreakCycle_WhenCircularDependencyExists()
    {
        // Arrange: A depends on B, B depends on A
        var opA = CreateOperation("GET", "/api/a");
        var opB = CreateOperation("POST", "/api/b");

        var operations = new[] { opA, opB };
        var edges = new[]
        {
            new DependencyEdge
            {
                SourceOperationId = opA.OperationId,
                TargetOperationId = opB.OperationId,
                Type = DependencyEdgeType.OperationSchema,
                Confidence = 1.0,
            },
            new DependencyEdge
            {
                SourceOperationId = opB.OperationId,
                TargetOperationId = opA.OperationId,
                Type = DependencyEdgeType.OperationSchema,
                Confidence = 1.0,
            },
        };

        // Act
        var result = _sut.Sort(operations, edges);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.IsCycleBreak);
        result.First(r => r.IsCycleBreak).ReasonCodes.Should().Contain("CYCLE_BREAK_FALLBACK");
    }

    [Fact]
    public void Sort_Should_BreakCycleDeterministically_BasedOnRanking()
    {
        // Arrange: A ↔ B cycle, B is POST (higher priority)
        var opA = CreateOperation("GET", "/api/projects");
        var opB = CreateOperation("POST", "/api/projects");

        var operations = new[] { opA, opB };
        var edges = new[]
        {
            new DependencyEdge
            {
                SourceOperationId = opA.OperationId,
                TargetOperationId = opB.OperationId,
                Confidence = 1.0,
            },
            new DependencyEdge
            {
                SourceOperationId = opB.OperationId,
                TargetOperationId = opA.OperationId,
                Confidence = 1.0,
            },
        };

        // Act
        var result = _sut.Sort(operations, edges);

        // Assert: POST should be selected as cycle breaker (higher method weight)
        result.Should().HaveCount(2);
        result[0].OperationId.Should().Be(opB.OperationId); // POST first
        result[0].IsCycleBreak.Should().BeTrue();
    }

    #endregion

    #region Auth-First Priority Tests

    [Fact]
    public void Sort_Should_PrioritizeAuthRelatedOperations()
    {
        // Arrange
        var authLogin = CreateOperation("POST", "/api/auth/login", isAuthRelated: true);
        var getUsers = CreateOperation("GET", "/api/users");
        var postOrders = CreateOperation("POST", "/api/orders");

        var operations = new[] { getUsers, postOrders, authLogin };

        // Act
        var result = _sut.Sort(operations, Array.Empty<DependencyEdge>());

        // Assert: Auth should always be first
        result[0].OperationId.Should().Be(authLogin.OperationId);
        result[0].ReasonCodes.Should().Contain("AUTH_FIRST");
    }

    [Fact]
    public void Sort_Should_PrioritizeAuth_EvenWithLowerFanOut()
    {
        // Arrange: non-auth has higher fan-out but auth should still be first
        var authLogin = CreateOperation("POST", "/api/auth/login", isAuthRelated: true);
        var postUsers = CreateOperation("POST", "/api/users");
        var getUser = CreateOperation("GET", "/api/users/{id}");
        var updateUser = CreateOperation("PUT", "/api/users/{id}");
        var deleteUser = CreateOperation("DELETE", "/api/users/{id}");

        var operations = new[] { authLogin, postUsers, getUser, updateUser, deleteUser };
        // postUsers has fan-out of 3 (GET, PUT, DELETE depend on it)
        var edges = new[]
        {
            CreateEdge(getUser.OperationId, postUsers.OperationId),
            CreateEdge(updateUser.OperationId, postUsers.OperationId),
            CreateEdge(deleteUser.OperationId, postUsers.OperationId),
        };

        // Act
        var result = _sut.Sort(operations, edges);

        // Assert: Auth still first
        result[0].OperationId.Should().Be(authLogin.OperationId);
    }

    #endregion

    #region Fan-Out Ranking Tests (KAT Enhancement)

    [Fact]
    public void Sort_Should_PrioritizeHigherFanOut_WhenNoOtherDifferentiator()
    {
        // Arrange: Posts are producers, higher fan-out should come first
        var postCategories = CreateOperation("POST", "/api/categories");
        var postProducts = CreateOperation("POST", "/api/products");
        var getProduct = CreateOperation("GET", "/api/products/{id}");
        var updateProduct = CreateOperation("PUT", "/api/products/{id}");
        var deleteProduct = CreateOperation("DELETE", "/api/products/{id}");

        var operations = new[] { postCategories, postProducts, getProduct, updateProduct, deleteProduct };
        // postProducts has fan-out of 3
        var edges = new[]
        {
            CreateEdge(getProduct.OperationId, postProducts.OperationId),
            CreateEdge(updateProduct.OperationId, postProducts.OperationId),
            CreateEdge(deleteProduct.OperationId, postProducts.OperationId),
        };

        // Act
        var result = _sut.Sort(operations, edges);

        // Assert: postProducts (fan-out 3) should come before postCategories (fan-out 0)
        var orderMap = result.ToDictionary(r => r.OperationId, r => r.OrderIndex);
        orderMap[postProducts.OperationId].Should().BeLessThan(orderMap[postCategories.OperationId]);

        // FanOut should be recorded
        result.First(r => r.OperationId == postProducts.OperationId).FanOut.Should().Be(3);
        result.First(r => r.OperationId == postProducts.OperationId).ReasonCodes.Should().Contain("HIGH_FAN_OUT");
    }

    #endregion

    #region HTTP Method Weight Tests

    [Fact]
    public void Sort_Should_OrderByMethodWeight_PostBeforeGetBeforeDelete()
    {
        // Arrange: Same path, different methods, no dependencies
        var getUsers = CreateOperation("GET", "/api/users");
        var postUsers = CreateOperation("POST", "/api/users");
        var deleteUsers = CreateOperation("DELETE", "/api/users");

        var operations = new[] { deleteUsers, getUsers, postUsers };

        // Act
        var result = _sut.Sort(operations, Array.Empty<DependencyEdge>());

        // Assert: POST → GET → DELETE
        result[0].OperationId.Should().Be(postUsers.OperationId);
        result[1].OperationId.Should().Be(getUsers.OperationId);
        result[2].OperationId.Should().Be(deleteUsers.OperationId);
    }

    #endregion

    #region Deterministic Ordering Tests

    [Fact]
    public void Sort_Should_ProduceDeterministicOutput_ForSameInput()
    {
        // Arrange
        var ops = Enumerable.Range(1, 10)
            .Select(i => CreateOperation("GET", $"/api/resource{i}"))
            .ToList();

        var edges = Array.Empty<DependencyEdge>();

        // Act: Run multiple times
        var results = Enumerable.Range(0, 5)
            .Select(_ => _sut.Sort(ops, edges))
            .ToList();

        // Assert: All results should be identical
        var firstResult = results[0].Select(r => r.OperationId).ToList();
        foreach (var otherResult in results.Skip(1))
        {
            otherResult.Select(r => r.OperationId).Should().BeEquivalentTo(firstResult, opt => opt.WithStrictOrdering());
        }
    }

    [Fact]
    public void Sort_Should_OrderByPathAlphabetically_WhenOtherFactorsEqual()
    {
        // Arrange
        var opZ = CreateOperation("GET", "/api/z");
        var opA = CreateOperation("GET", "/api/a");
        var opM = CreateOperation("GET", "/api/m");

        var operations = new[] { opZ, opA, opM };

        // Act
        var result = _sut.Sort(operations, Array.Empty<DependencyEdge>());

        // Assert: Alphabetical by path
        result[0].OperationId.Should().Be(opA.OperationId);
        result[1].OperationId.Should().Be(opM.OperationId);
        result[2].OperationId.Should().Be(opZ.OperationId);
    }

    #endregion

    #region Duplicate Operation Tests

    [Fact]
    public void Sort_Should_DeduplicateOperations_ByOperationId()
    {
        // Arrange: Same operation ID appears twice
        var opId = Guid.NewGuid();
        var op1 = new SortableOperation { OperationId = opId, HttpMethod = "GET", Path = "/api/users" };
        var op2 = new SortableOperation { OperationId = opId, HttpMethod = "GET", Path = "/api/users" };

        var operations = new[] { op1, op2 };

        // Act
        var result = _sut.Sort(operations, Array.Empty<DependencyEdge>());

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region Helpers

    private static SortableOperation CreateOperation(string method, string path, bool isAuthRelated = false)
    {
        return new SortableOperation
        {
            OperationId = Guid.NewGuid(),
            HttpMethod = method,
            Path = path,
            IsAuthRelated = isAuthRelated,
        };
    }

    private static DependencyEdge CreateEdge(Guid sourceId, Guid targetId, double confidence = 1.0)
    {
        return new DependencyEdge
        {
            SourceOperationId = sourceId,
            TargetOperationId = targetId,
            Type = DependencyEdgeType.OperationSchema,
            Confidence = confidence,
        };
    }

    #endregion
}
