using ClassifiedAds.Modules.TestGeneration.Algorithms;
using ClassifiedAds.Modules.TestGeneration.Algorithms.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for SchemaRelationshipAnalyzer.
/// Covers: schema reference graph building, transitive closure, fuzzy matching,
/// and operation dependency detection.
/// Source: KAT paper (arXiv:2407.10227) Section 4.2 - Schema-Schema Dependencies.
/// </summary>
public class SchemaRelationshipAnalyzerTests
{
    private readonly SchemaRelationshipAnalyzer _sut;

    public SchemaRelationshipAnalyzerTests()
    {
        _sut = new SchemaRelationshipAnalyzer();
    }

    #region BuildSchemaReferenceGraph (New - Dictionary Overload)

    [Fact]
    public void BuildSchemaReferenceGraph_Should_ReturnEmpty_WhenInputIsNull()
    {
        // Act
        var result = _sut.BuildSchemaReferenceGraph((IReadOnlyDictionary<string, string>)null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildSchemaReferenceGraph_Should_ReturnEmpty_WhenInputIsEmpty()
    {
        // Act
        var result = _sut.BuildSchemaReferenceGraph(new Dictionary<string, string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildSchemaReferenceGraph_Should_CreateUnidirectionalEdges()
    {
        // Arrange: Order references User and Product
        var schemas = new Dictionary<string, string>
        {
            ["Order"] = @"{ ""properties"": { ""user"": { ""$ref"": ""#/components/schemas/User"" }, ""items"": { ""items"": { ""$ref"": ""#/components/schemas/Product"" } } } }",
            ["User"] = @"{ ""type"": ""object"", ""properties"": { ""name"": { ""type"": ""string"" } } }",
            ["Product"] = @"{ ""type"": ""object"", ""properties"": { ""price"": { ""type"": ""number"" } } }",
        };

        // Act
        var result = _sut.BuildSchemaReferenceGraph(schemas);

        // Assert: Order → User, Order → Product (unidirectional)
        result.Should().ContainKey("Order");
        result["Order"].Should().Contain("User");
        result["Order"].Should().Contain("Product");

        // User and Product should NOT point back to Order
        result["User"].Should().BeEmpty();
        result["Product"].Should().BeEmpty();
    }

    [Fact]
    public void BuildSchemaReferenceGraph_Should_IgnoreSelfReferences()
    {
        // Arrange: Node references itself
        var schemas = new Dictionary<string, string>
        {
            ["Node"] = @"{ ""properties"": { ""children"": { ""items"": { ""$ref"": ""#/components/schemas/Node"" } } } }",
        };

        // Act
        var result = _sut.BuildSchemaReferenceGraph(schemas);

        // Assert: Node should NOT have itself as reference
        result["Node"].Should().BeEmpty();
    }

    [Fact]
    public void BuildSchemaReferenceGraph_Should_HandleChainedReferences()
    {
        // Arrange: A → B → C
        var schemas = new Dictionary<string, string>
        {
            ["A"] = @"{ ""properties"": { ""b"": { ""$ref"": ""#/components/schemas/B"" } } }",
            ["B"] = @"{ ""properties"": { ""c"": { ""$ref"": ""#/components/schemas/C"" } } }",
            ["C"] = @"{ ""type"": ""object"" }",
        };

        // Act
        var result = _sut.BuildSchemaReferenceGraph(schemas);

        // Assert: Only direct references
        result["A"].Should().Contain("B");
        result["A"].Should().NotContain("C"); // C is transitive, not direct

        result["B"].Should().Contain("C");
        result["C"].Should().BeEmpty();
    }

    [Fact]
    public void BuildSchemaReferenceGraph_Should_HandleDefinitionsAltSyntax()
    {
        // Arrange: Uses #/definitions/ instead of #/components/schemas/
        var schemas = new Dictionary<string, string>
        {
            ["OrderDto"] = @"{ ""properties"": { ""user"": { ""$ref"": ""#/definitions/UserDto"" } } }",
            ["UserDto"] = @"{ ""type"": ""object"" }",
        };

        // Act
        var result = _sut.BuildSchemaReferenceGraph(schemas);

        // Assert
        result["OrderDto"].Should().Contain("UserDto");
    }

    #endregion

    #region BuildSchemaReferenceGraphLegacy (Co-reference Based)

    [Fact]
    public void BuildSchemaReferenceGraphLegacy_Should_ReturnEmpty_WhenInputIsNull()
    {
        // Act
        var result = _sut.BuildSchemaReferenceGraphLegacy(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildSchemaReferenceGraphLegacy_Should_ReturnEmpty_WhenInputIsEmpty()
    {
        // Act
        var result = _sut.BuildSchemaReferenceGraphLegacy(Array.Empty<string>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void BuildSchemaReferenceGraphLegacy_Should_CreateBidirectionalEdges_ForCoReferences()
    {
        // Arrange: Payload references both User and Product
        var payloads = new[]
        {
            @"{ ""properties"": { ""user"": { ""$ref"": ""#/components/schemas/User"" }, ""product"": { ""$ref"": ""#/components/schemas/Product"" } } }",
        };

        // Act
        var result = _sut.BuildSchemaReferenceGraphLegacy(payloads);

        // Assert: Bidirectional co-reference
        result["User"].Should().Contain("Product");
        result["Product"].Should().Contain("User");
    }

    [Fact]
    public void BuildSchemaReferenceGraphLegacy_Should_HandleSingleRefPayload()
    {
        // Arrange: Bare single ref
        var payloads = new[]
        {
            @"{ ""$ref"": ""#/components/schemas/User"" }",
        };

        // Act
        var result = _sut.BuildSchemaReferenceGraphLegacy(payloads);

        // Assert: User exists but has no edges (single ref in bare payload)
        result.Should().ContainKey("User");
        result["User"].Should().BeEmpty();
    }

    #endregion

    #region ComputeTransitiveClosure Tests

    [Fact]
    public void ComputeTransitiveClosure_Should_ReturnEmpty_WhenInputIsNull()
    {
        // Act
        var result = _sut.ComputeTransitiveClosure(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeTransitiveClosure_Should_ReturnEmpty_WhenInputIsEmpty()
    {
        // Act
        var result = _sut.ComputeTransitiveClosure(new Dictionary<string, HashSet<string>>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ComputeTransitiveClosure_Should_IncludeTransitiveReferences()
    {
        // Arrange: A → B → C
        var directGraph = new Dictionary<string, HashSet<string>>
        {
            ["A"] = new(StringComparer.OrdinalIgnoreCase) { "B" },
            ["B"] = new(StringComparer.OrdinalIgnoreCase) { "C" },
            ["C"] = new(StringComparer.OrdinalIgnoreCase),
        };

        // Act
        var result = _sut.ComputeTransitiveClosure(directGraph);

        // Assert: A should reach both B and C
        result["A"].Should().Contain("B");
        result["A"].Should().Contain("C");

        // B should reach C
        result["B"].Should().Contain("C");

        // C has no outgoing
        result["C"].Should().BeEmpty();
    }

    [Fact]
    public void ComputeTransitiveClosure_Should_HandleDiamondPattern()
    {
        // Arrange: A → B, A → C, B → D, C → D
        var directGraph = new Dictionary<string, HashSet<string>>
        {
            ["A"] = new(StringComparer.OrdinalIgnoreCase) { "B", "C" },
            ["B"] = new(StringComparer.OrdinalIgnoreCase) { "D" },
            ["C"] = new(StringComparer.OrdinalIgnoreCase) { "D" },
            ["D"] = new(StringComparer.OrdinalIgnoreCase),
        };

        // Act
        var result = _sut.ComputeTransitiveClosure(directGraph);

        // Assert: A should reach B, C, D
        result["A"].Should().HaveCount(3);
        result["A"].Should().Contain("B");
        result["A"].Should().Contain("C");
        result["A"].Should().Contain("D");
    }

    [Fact]
    public void ComputeTransitiveClosure_Should_HandleCycles()
    {
        // Arrange: A → B → C → A (cycle)
        var directGraph = new Dictionary<string, HashSet<string>>
        {
            ["A"] = new(StringComparer.OrdinalIgnoreCase) { "B" },
            ["B"] = new(StringComparer.OrdinalIgnoreCase) { "C" },
            ["C"] = new(StringComparer.OrdinalIgnoreCase) { "A" },
        };

        // Act
        var result = _sut.ComputeTransitiveClosure(directGraph);

        // Assert: All nodes should reach all other nodes (strongly connected)
        result["A"].Should().Contain("B");
        result["A"].Should().Contain("C");
        result["B"].Should().Contain("A");
        result["B"].Should().Contain("C");
        result["C"].Should().Contain("A");
        result["C"].Should().Contain("B");
    }

    #endregion

    #region FindTransitiveSchemaDependencies Tests

    [Fact]
    public void FindTransitiveSchemaDependencies_Should_ReturnEmpty_WhenParameterRefsIsNull()
    {
        // Arrange
        var responseRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [Guid.NewGuid()] = new[] { "User" },
        };
        var transitiveGraph = new Dictionary<string, HashSet<string>>
        {
            ["User"] = new(StringComparer.OrdinalIgnoreCase),
        };

        // Act
        var result = _sut.FindTransitiveSchemaDependencies(null, responseRefs, transitiveGraph);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindTransitiveSchemaDependencies_Should_ReturnEmpty_WhenTransitiveGraphIsEmpty()
    {
        // Arrange
        var paramRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [Guid.NewGuid()] = new[] { "OrderRequest" },
        };
        var responseRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [Guid.NewGuid()] = new[] { "User" },
        };

        // Act
        var result = _sut.FindTransitiveSchemaDependencies(
            paramRefs, responseRefs, new Dictionary<string, HashSet<string>>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindTransitiveSchemaDependencies_Should_FindTransitiveProducer()
    {
        // Arrange
        var consumerId = Guid.NewGuid();
        var producerId = Guid.NewGuid();

        // Consumer uses OrderRequest which references User
        var paramRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [consumerId] = new[] { "OrderRequest" },
        };

        // Producer produces User
        var responseRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [producerId] = new[] { "User" },
        };

        // OrderRequest → User (transitive)
        var transitiveGraph = new Dictionary<string, HashSet<string>>
        {
            ["OrderRequest"] = new(StringComparer.OrdinalIgnoreCase) { "User" },
            ["User"] = new(StringComparer.OrdinalIgnoreCase),
        };

        // Act
        var result = _sut.FindTransitiveSchemaDependencies(paramRefs, responseRefs, transitiveGraph);

        // Assert
        result.Should().HaveCount(1);
        result.First().SourceOperationId.Should().Be(consumerId);
        result.First().TargetOperationId.Should().Be(producerId);
        result.First().Type.Should().Be(DependencyEdgeType.SchemaSchema);
        result.First().Confidence.Should().Be(0.85);
    }

    [Fact]
    public void FindTransitiveSchemaDependencies_Should_SkipSelfDependency()
    {
        // Arrange: Same operation is both consumer and producer
        var opId = Guid.NewGuid();

        var paramRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [opId] = new[] { "OrderRequest" },
        };
        var responseRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [opId] = new[] { "User" },
        };
        var transitiveGraph = new Dictionary<string, HashSet<string>>
        {
            ["OrderRequest"] = new(StringComparer.OrdinalIgnoreCase) { "User" },
            ["User"] = new(StringComparer.OrdinalIgnoreCase),
        };

        // Act
        var result = _sut.FindTransitiveSchemaDependencies(paramRefs, responseRefs, transitiveGraph);

        // Assert: No self-dependency edge
        result.Should().BeEmpty();
    }

    #endregion

    #region FindFuzzySchemaNameDependencies Tests

    [Fact]
    public void FindFuzzySchemaNameDependencies_Should_ReturnEmpty_WhenInputIsNull()
    {
        // Act
        var result = _sut.FindFuzzySchemaNameDependencies(null, null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindFuzzySchemaNameDependencies_Should_MatchByBaseName()
    {
        // Arrange
        var consumerId = Guid.NewGuid();
        var producerId = Guid.NewGuid();

        // Consumer uses CreateUserRequest
        var paramRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [consumerId] = new[] { "CreateUserRequest" },
        };

        // Producer produces UserResponse
        var responseRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [producerId] = new[] { "UserResponse" },
        };

        // Act
        var result = _sut.FindFuzzySchemaNameDependencies(paramRefs, responseRefs);

        // Assert: Both share base name "User"
        result.Should().HaveCount(1);
        result.First().SourceOperationId.Should().Be(consumerId);
        result.First().TargetOperationId.Should().Be(producerId);
        result.First().Confidence.Should().Be(0.65);
        result.First().Reason.Should().Contain("User");
    }

    [Fact]
    public void FindFuzzySchemaNameDependencies_Should_SkipExactMatch()
    {
        // Arrange: Same schema name (already handled by Rule 2)
        var consumerId = Guid.NewGuid();
        var producerId = Guid.NewGuid();

        var paramRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [consumerId] = new[] { "User" },
        };
        var responseRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [producerId] = new[] { "User" },
        };

        // Act
        var result = _sut.FindFuzzySchemaNameDependencies(paramRefs, responseRefs);

        // Assert: Exact match skipped
        result.Should().BeEmpty();
    }

    [Fact]
    public void FindFuzzySchemaNameDependencies_Should_SkipSelfDependency()
    {
        // Arrange
        var opId = Guid.NewGuid();

        var paramRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [opId] = new[] { "CreateUserRequest" },
        };
        var responseRefs = new Dictionary<Guid, IReadOnlyCollection<string>>
        {
            [opId] = new[] { "UserResponse" },
        };

        // Act
        var result = _sut.FindFuzzySchemaNameDependencies(paramRefs, responseRefs);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region ExtractSchemaRefsFromPayload Tests

    [Fact]
    public void ExtractSchemaRefsFromPayload_Should_ReturnEmpty_WhenPayloadIsNull()
    {
        // Act
        var result = SchemaRelationshipAnalyzer.ExtractSchemaRefsFromPayload(null);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExtractSchemaRefsFromPayload_Should_ExtractComponentsSchemaRefs()
    {
        // Arrange
        var payload = @"{ ""$ref"": ""#/components/schemas/User"" }";

        // Act
        var result = SchemaRelationshipAnalyzer.ExtractSchemaRefsFromPayload(payload);

        // Assert
        result.Should().Contain("User");
    }

    [Fact]
    public void ExtractSchemaRefsFromPayload_Should_ExtractDefinitionsRefs()
    {
        // Arrange
        var payload = @"{ ""$ref"": ""#/definitions/OrderDto"" }";

        // Act
        var result = SchemaRelationshipAnalyzer.ExtractSchemaRefsFromPayload(payload);

        // Assert
        result.Should().Contain("OrderDto");
    }

    [Fact]
    public void ExtractSchemaRefsFromPayload_Should_ExtractMultipleRefs()
    {
        // Arrange
        var payload = @"{
            ""properties"": {
                ""user"": { ""$ref"": ""#/components/schemas/User"" },
                ""items"": { ""items"": { ""$ref"": ""#/components/schemas/Product"" } },
                ""category"": { ""$ref"": ""#/definitions/Category"" }
            }
        }";

        // Act
        var result = SchemaRelationshipAnalyzer.ExtractSchemaRefsFromPayload(payload);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("User");
        result.Should().Contain("Product");
        result.Should().Contain("Category");
    }

    #endregion

    #region ExtractSchemaBaseName Tests

    [Fact]
    public void ExtractSchemaBaseName_Should_ReturnNull_WhenInputIsNull()
    {
        // Act
        var result = SchemaRelationshipAnalyzer.ExtractSchemaBaseName(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractSchemaBaseName_Should_StripRequestSuffix()
    {
        // Act
        var result = SchemaRelationshipAnalyzer.ExtractSchemaBaseName("CreateUserRequest");

        // Assert
        result.Should().Be("User");
    }

    [Fact]
    public void ExtractSchemaBaseName_Should_StripResponseSuffix()
    {
        // Act
        var result = SchemaRelationshipAnalyzer.ExtractSchemaBaseName("UserResponse");

        // Assert
        result.Should().Be("User");
    }

    [Fact]
    public void ExtractSchemaBaseName_Should_StripDtoSuffix()
    {
        // Act
        var result = SchemaRelationshipAnalyzer.ExtractSchemaBaseName("OrderItemDto");

        // Assert
        result.Should().Be("OrderItem");
    }

    [Fact]
    public void ExtractSchemaBaseName_Should_StripPrefixesAndSuffixes()
    {
        // Act
        var result = SchemaRelationshipAnalyzer.ExtractSchemaBaseName("CreateOrderCommand");

        // Assert
        result.Should().Be("Order");
    }

    [Fact]
    public void ExtractSchemaBaseName_Should_ReturnNull_WhenResultTooShort()
    {
        // Act
        var result = SchemaRelationshipAnalyzer.ExtractSchemaBaseName("ADto");

        // Assert
        result.Should().BeNull(); // "A" is too short
    }

    #endregion
}
