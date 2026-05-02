using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Services;
using System.Collections.Generic;

namespace ClassifiedAds.UnitTests.TestGeneration;

/// <summary>
/// Unit tests for ErrorResponseSchemaAnalyzer.
/// Validates field extraction and JSONPath assertion building from Swagger error response schemas.
/// </summary>
public class ErrorResponseSchemaAnalyzerTests
{
    [Fact]
    public void ExtractFieldNames_Should_ReturnTopLevelProperties_FromObjectSchema()
    {
        var schema = """
        {
          "type": "object",
          "properties": {
            "success": { "type": "boolean" },
            "message": { "type": "string" },
            "errors": { "type": "array" }
          }
        }
        """;

        var result = ErrorResponseSchemaAnalyzer.ExtractFieldNames(schema);

        result.Should().BeEquivalentTo(new[] { "success", "message", "errors" });
    }

    [Fact]
    public void ExtractFieldNames_Should_ReturnProperties_FromAllOfSchema()
    {
        var schema = """
        {
          "allOf": [
            {
              "type": "object",
              "properties": {
                "code": { "type": "integer" }
              }
            },
            {
              "type": "object",
              "properties": {
                "detail": { "type": "string" }
              }
            }
          ]
        }
        """;

        var result = ErrorResponseSchemaAnalyzer.ExtractFieldNames(schema);

        result.Should().Contain("code");
        result.Should().Contain("detail");
    }

    [Fact]
    public void ExtractFieldNames_Should_ReturnFirstVariant_FromOneOfSchema()
    {
        var schema = """
        {
          "oneOf": [
            {
              "type": "object",
              "properties": {
                "error": { "type": "string" },
                "status": { "type": "integer" }
              }
            },
            {
              "type": "object",
              "properties": {
                "message": { "type": "string" }
              }
            }
          ]
        }
        """;

        var result = ErrorResponseSchemaAnalyzer.ExtractFieldNames(schema);

        result.Should().Contain("error");
        result.Should().Contain("status");
        result.Should().NotContain("message");
    }

    [Fact]
    public void ExtractFieldNames_Should_ReturnEmptyList_WhenSchemaIsNull()
    {
        ErrorResponseSchemaAnalyzer.ExtractFieldNames(null).Should().BeEmpty();
    }

    [Fact]
    public void ExtractFieldNames_Should_ReturnEmptyList_WhenSchemaIsInvalidJson()
    {
        ErrorResponseSchemaAnalyzer.ExtractFieldNames("not json").Should().BeEmpty();
    }

    [Fact]
    public void ExtractFieldNames_Should_ReturnEmptyList_WhenSchemaHasNoProperties()
    {
        var schema = """{"type": "string"}""";
        ErrorResponseSchemaAnalyzer.ExtractFieldNames(schema).Should().BeEmpty();
    }

    [Fact]
    public void ExtractFieldNames_Should_LimitToFiveFields()
    {
        var schema = """
        {
          "type": "object",
          "properties": {
            "a": { "type": "string" },
            "b": { "type": "string" },
            "c": { "type": "string" },
            "d": { "type": "string" },
            "e": { "type": "string" },
            "f": { "type": "string" },
            "g": { "type": "string" }
          }
        }
        """;

        var result = ErrorResponseSchemaAnalyzer.ExtractFieldNames(schema);
        result.Should().HaveCount(5);
    }

    [Fact]
    public void BuildJsonPathAssertions_Should_AssertSuccessFalse_ForBoundaryTest()
    {
        var schema = """
        {
          "type": "object",
          "properties": {
            "success": { "type": "boolean" },
            "message": { "type": "string" }
          }
        }
        """;

        var result = ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(schema, TestType.Boundary);

        result.Should().ContainKey("$.success");
        result["$.success"].Should().Be("false");
        result.Should().ContainKey("$.message");
        result["$.message"].Should().Be("*");
    }

    [Fact]
    public void BuildJsonPathAssertions_Should_AssertSuccessFalse_ForNegativeTest()
    {
        var schema = """
        {
          "type": "object",
          "properties": {
            "success": { "type": "boolean" },
            "detail": { "type": "string" }
          }
        }
        """;

        var result = ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(schema, TestType.Negative);

        result.Should().ContainKey("$.success");
        result["$.success"].Should().Be("false");
    }

    [Fact]
    public void BuildJsonPathAssertions_Should_NotAssertSuccessFalse_ForHappyPathTest()
    {
        var schema = """
        {
          "type": "object",
          "properties": {
            "success": { "type": "boolean" },
            "data": { "type": "object" }
          }
        }
        """;

        var result = ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(schema, TestType.HappyPath);

        result.Should().ContainKey("$.success");
        result["$.success"].Should().Be("*"); // HappyPath: do not assert false
    }

    [Fact]
    public void BuildJsonPathAssertions_Should_LimitToTwoAssertions()
    {
        var schema = """
        {
          "type": "object",
          "properties": {
            "success": { "type": "boolean" },
            "message": { "type": "string" },
            "errors": { "type": "array" },
            "code": { "type": "integer" }
          }
        }
        """;

        var result = ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(schema, TestType.Boundary);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void BuildJsonPathAssertions_Should_PrioritizeKnownFields()
    {
        var schema = """
        {
          "type": "object",
          "properties": {
            "timestamp": { "type": "string" },
            "requestId": { "type": "string" },
            "message": { "type": "string" },
            "success": { "type": "boolean" }
          }
        }
        """;

        var result = ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(schema, TestType.Boundary);

        // "success" and "message" should be prioritized over "timestamp" and "requestId"
        result.Should().ContainKey("$.success");
        result.Should().ContainKey("$.message");
        result.Should().NotContainKey("$.timestamp");
    }

    [Fact]
    public void BuildJsonPathAssertions_Should_ReturnEmpty_WhenSchemaIsNull()
    {
        ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(null, TestType.Boundary).Should().BeEmpty();
    }

    [Fact]
    public void BuildJsonPathAssertions_Should_PreservePascalCasing_FromSchema()
    {
        var schema = """
        {
          "type": "object",
          "properties": {
            "Success": { "type": "boolean" },
            "Message": { "type": "string" }
          }
        }
        """;

        var result = ErrorResponseSchemaAnalyzer.BuildJsonPathAssertions(schema, TestType.Boundary);

        // Should preserve PascalCase from schema
        result.Should().ContainKey("$.Success");
        result["$.Success"].Should().Be("false"); // case-insensitive match to "success"
    }
}
