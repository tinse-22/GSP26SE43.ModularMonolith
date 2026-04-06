using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.UnitTests.TestExecution;

public class RuleBasedValidatorTests
{
    private readonly RuleBasedValidator _validator;

    public RuleBasedValidatorTests()
    {
        _validator = new RuleBasedValidator(new Mock<ILogger<RuleBasedValidator>>().Object);
    }

    #region Transport Error

    [Fact]
    public void Validate_TransportError_Should_ShortCircuitWithHttpRequestError()
    {
        // Arrange
        var response = new HttpTestResponse
        {
            TransportError = "Connection refused",
        };
        var testCase = CreateTestCase();

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.StatusCodeMatched.Should().BeFalse();
        result.Failures.Should().HaveCount(1);
        result.Failures[0].Code.Should().Be("HTTP_REQUEST_ERROR");
        result.Failures[0].Message.Should().Be("Connection refused");
    }

    #endregion

    #region Status Code

    [Fact]
    public void Validate_StatusCodeMismatch_Should_Fail()
    {
        // Arrange
        var response = CreateResponse(statusCode: 404);
        var testCase = CreateTestCase(expectedStatus: "[200]");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.StatusCodeMatched.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "STATUS_CODE_MISMATCH");
        result.Failures[0].Expected.Should().Be("200");
        result.Failures[0].Actual.Should().Be("404");
    }

    [Fact]
    public void Validate_StatusCodeMultipleAllowed_Should_PassWhenMatchesAny()
    {
        // Arrange
        var response = CreateResponse(statusCode: 201);
        var testCase = CreateTestCase(expectedStatus: "[200, 201]");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.StatusCodeMatched.Should().BeTrue();
        result.Failures.Should().BeEmpty();
    }

    #endregion

    #region Response Schema

    [Fact]
    public void Validate_SchemaMismatch_Should_Fail()
    {
        // Arrange
        var response = CreateResponse(body: """{"name": "test"}""");
        var schema = """{"type": "object", "required": ["name", "age"], "properties": {"name": {"type": "string"}, "age": {"type": "integer"}}}""";
        var testCase = CreateTestCase(responseSchema: schema);

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.SchemaMatched.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "RESPONSE_SCHEMA_MISMATCH");
    }

    [Fact]
    public void Validate_SchemaMatch_Should_Pass()
    {
        // Arrange
        var response = CreateResponse(body: """{"name": "test", "age": 25}""");
        var schema = """{"type": "object", "required": ["name", "age"], "properties": {"name": {"type": "string"}, "age": {"type": "integer"}}}""";
        var testCase = CreateTestCase(responseSchema: schema);

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.SchemaMatched.Should().BeTrue();
    }

    [Fact]
    public void Validate_SchemaWithEnum_ShouldFailWhenValueNotInEnum()
    {
        // Arrange
        var response = CreateResponse(body: """{"status": "archived"}""");
        var schema = """{"type": "object", "required": ["status"], "properties": {"status": {"type": "string", "enum": ["active", "inactive"]}}}""";
        var testCase = CreateTestCase(responseSchema: schema);

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.SchemaMatched.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "RESPONSE_SCHEMA_MISMATCH");
    }

    [Fact]
    public void Validate_SchemaWithNullable_ShouldPassWhenNull()
    {
        // Arrange
        var response = CreateResponse(body: """{"middleName": null}""");
        var schema = """{"type": "object", "required": ["middleName"], "properties": {"middleName": {"type": "string", "nullable": true}}}""";
        var testCase = CreateTestCase(responseSchema: schema);

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.SchemaMatched.Should().BeTrue();
    }

    [Fact]
    public void Validate_SchemaFallbackFromEndpointMetadata_Should_BeUsed()
    {
        // Arrange
        var response = CreateResponse(body: """{"name": "test"}""");
        var fallbackSchema = """{"type": "object", "required": ["name", "email"], "properties": {"name": {"type": "string"}, "email": {"type": "string"}}}""";
        var testCase = CreateTestCase(responseSchema: null);
        var metadata = new ApiEndpointMetadataDto
        {
            EndpointId = Guid.NewGuid(),
            ResponseSchemaPayloads = new[] { fallbackSchema },
        };

        // Act
        var result = _validator.Validate(response, testCase, metadata);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.SchemaMatched.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "RESPONSE_SCHEMA_MISMATCH");
    }

    [Fact]
    public void Validate_EmptyBodyWithSchema_Should_FailWithResponseNotJson()
    {
        // Arrange
        var response = CreateResponse(body: null);
        var schema = """{"type": "object"}""";
        var testCase = CreateTestCase(responseSchema: schema);

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.SchemaMatched.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "RESPONSE_NOT_JSON");
    }

    #endregion

    #region Header Checks

    [Fact]
    public void Validate_HeaderMismatch_Should_Fail()
    {
        // Arrange
        var response = CreateResponse(headers: new Dictionary<string, string>
        {
            ["Content-Type"] = "text/html",
        });
        var testCase = CreateTestCase(headerChecks: """{"Content-Type": "application/json"}""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.HeaderChecksPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "HEADER_MISMATCH");
        result.Failures[0].Target.Should().Be("Content-Type");
        result.Failures[0].Expected.Should().Be("application/json");
        result.Failures[0].Actual.Should().Be("text/html");
    }

    [Fact]
    public void Validate_HeaderMissing_Should_Fail()
    {
        // Arrange
        var response = CreateResponse(headers: new Dictionary<string, string>());
        var testCase = CreateTestCase(headerChecks: """{"X-Custom-Header": "value"}""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.HeaderChecksPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "HEADER_MISMATCH");
    }

    [Fact]
    public void Validate_HeaderCaseInsensitiveMatch_Should_Pass()
    {
        // Arrange
        var response = CreateResponse(headers: new Dictionary<string, string>
        {
            ["content-type"] = "application/json",
        });
        var testCase = CreateTestCase(headerChecks: """{"Content-Type": "application/json"}""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.HeaderChecksPassed.Should().BeTrue();
    }

    #endregion

    #region Body Contains / Not Contains

    [Fact]
    public void Validate_BodyContainsMissing_Should_Fail()
    {
        // Arrange
        var response = CreateResponse(body: """{"status": "active"}""");
        var testCase = CreateTestCase(bodyContains: """["success", "completed"]""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.BodyContainsPassed.Should().BeFalse();
        result.Failures.Should().HaveCount(2);
        result.Failures.Should().OnlyContain(f => f.Code == "BODY_CONTAINS_MISSING");
    }

    [Fact]
    public void Validate_BodyContainsPresent_Should_Pass()
    {
        // Arrange
        var response = CreateResponse(body: """{"status": "active", "message": "success"}""");
        var testCase = CreateTestCase(bodyContains: """["active", "success"]""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.BodyContainsPassed.Should().BeTrue();
    }

    [Fact]
    public void Validate_BodyNotContainsPresent_Should_Fail()
    {
        // Arrange
        var response = CreateResponse(body: """{"error": "forbidden"}""");
        var testCase = CreateTestCase(bodyNotContains: """["forbidden"]""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.BodyNotContainsPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "BODY_NOT_CONTAINS_PRESENT");
    }

    [Fact]
    public void Validate_BodyNotContainsAbsent_Should_Pass()
    {
        // Arrange
        var response = CreateResponse(body: """{"status": "ok"}""");
        var testCase = CreateTestCase(bodyNotContains: """["error", "forbidden"]""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.BodyNotContainsPassed.Should().BeTrue();
    }

    #endregion

    #region JSONPath Checks

    [Fact]
    public void Validate_JsonPathAssertionFailed_Should_Fail()
    {
        // Arrange
        var response = CreateResponse(body: """{"user": {"name": "Alice", "age": 30}}""");
        var testCase = CreateTestCase(jsonPathChecks: """{"$.user.name": "Bob"}""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.JsonPathChecksPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "JSONPATH_ASSERTION_FAILED");
        result.Failures[0].Target.Should().Be("$.user.name");
        result.Failures[0].Expected.Should().Be("Bob");
        result.Failures[0].Actual.Should().Be("Alice");
    }

    [Fact]
    public void Validate_JsonPathEquality_Should_Pass()
    {
        // Arrange
        var response = CreateResponse(body: """{"user": {"name": "Alice", "age": 30}}""");
        var testCase = CreateTestCase(jsonPathChecks: """{"$.user.name": "Alice", "$.user.age": "30"}""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.JsonPathChecksPassed.Should().BeTrue();
    }

    [Fact]
    public void Validate_JsonPathArrayAccess_Should_Work()
    {
        // Arrange
        var response = CreateResponse(body: """{"items": [{"id": 1}, {"id": 2}]}""");
        var testCase = CreateTestCase(jsonPathChecks: """{"$.items[0].id": "1"}""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.JsonPathChecksPassed.Should().BeTrue();
    }

    [Fact]
    public void Validate_JsonPathMissingPath_Should_Fail()
    {
        // Arrange
        var response = CreateResponse(body: """{"user": {"name": "Alice"}}""");
        var testCase = CreateTestCase(jsonPathChecks: """{"$.user.email": "alice@test.com"}""");

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.JsonPathChecksPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "JSONPATH_ASSERTION_FAILED");
        result.Failures[0].Actual.Should().BeNull();
    }

    #endregion

    #region Response Time

    [Fact]
    public void Validate_ResponseTimeExceeded_Should_Fail()
    {
        // Arrange
        var response = CreateResponse(latencyMs: 5000);
        var testCase = CreateTestCase(maxResponseTime: 2000);

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.ResponseTimePassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "RESPONSE_TIME_EXCEEDED");
        result.Failures[0].Expected.Should().Be("2000ms");
        result.Failures[0].Actual.Should().Be("5000ms");
    }

    [Fact]
    public void Validate_ResponseTimeWithinLimit_Should_Pass()
    {
        // Arrange
        var response = CreateResponse(latencyMs: 500);
        var testCase = CreateTestCase(maxResponseTime: 2000);

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.ResponseTimePassed.Should().BeTrue();
    }

    #endregion

    #region No Expectation

    [Fact]
    public void Validate_NoExpectation_Should_PassWithWarning()
    {
        // Arrange
        var response = CreateResponse();
        var testCase = new ExecutionTestCaseDto
        {
            TestCaseId = Guid.NewGuid(),
            Expectation = null,
        };

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Code == "NO_EXPECTATION_DEFINED");
        result.ChecksPerformed.Should().Be(0);
        result.ChecksSkipped.Should().Be(7);
    }

    [Fact]
    public void Validate_NoExpectation_StrictMode_Should_Fail()
    {
        // Arrange
        var response = CreateResponse();
        var testCase = new ExecutionTestCaseDto
        {
            TestCaseId = Guid.NewGuid(),
            Expectation = null,
        };

        // Act
        var result = _validator.Validate(response, testCase, strictMode: true);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f => f.Code == "NO_EXPECTATION");
        result.Warnings.Should().BeEmpty();
        result.ChecksPerformed.Should().Be(0);
        result.ChecksSkipped.Should().Be(7);
    }

    [Fact]
    public void Validate_AllChecksSkipped_Should_PassWithWarning()
    {
        // Arrange
        var response = CreateResponse();
        var testCase = CreateTestCase(
            expectedStatus: string.Empty,
            responseSchema: string.Empty,
            headerChecks: string.Empty,
            bodyContains: string.Empty,
            bodyNotContains: string.Empty,
            jsonPathChecks: string.Empty,
            maxResponseTime: null);

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.Code == "ALL_CHECKS_SKIPPED");
        result.ChecksPerformed.Should().Be(0);
        result.ChecksSkipped.Should().Be(7);
    }

    #endregion

    #region Expectation Format

    [Theory]
    [InlineData("abc", null, null, null, null, "ExpectedStatus")]
    [InlineData(null, "not-json", null, null, null, "HeaderChecks")]
    [InlineData(null, null, "not-json", null, null, "BodyContains")]
    [InlineData(null, null, null, "not-json", null, "BodyNotContains")]
    [InlineData(null, null, null, null, "not-json", "JsonPathChecks")]
    public void Validate_InvalidExpectationFormat_Should_Fail(
        string? expectedStatus,
        string? headerChecks,
        string? bodyContains,
        string? bodyNotContains,
        string? jsonPathChecks,
        string expectedTarget)
    {
        // Arrange
        var response = CreateResponse(body: "{}", headers: new Dictionary<string, string>());
        var testCase = CreateTestCase(
            expectedStatus: expectedStatus,
            headerChecks: headerChecks,
            bodyContains: bodyContains,
            bodyNotContains: bodyNotContains,
            jsonPathChecks: jsonPathChecks);

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Failures.Should().ContainSingle(f =>
            f.Code == "INVALID_EXPECTATION_FORMAT" &&
            f.Target == expectedTarget);
    }

    #endregion

    #region Multiple Failures

    [Fact]
    public void Validate_MultipleFailures_Should_CollectAll()
    {
        // Arrange
        var response = CreateResponse(
            statusCode: 500,
            body: """{"error": "internal"}""",
            latencyMs: 10000);
        var testCase = CreateTestCase(
            expectedStatus: "[200]",
            bodyContains: """["success"]""",
            maxResponseTime: 3000);

        // Act
        var result = _validator.Validate(response, testCase);

        // Assert
        result.IsPassed.Should().BeFalse();
        result.Failures.Should().HaveCount(3);
        result.Failures.Should().Contain(f => f.Code == "STATUS_CODE_MISMATCH");
        result.Failures.Should().Contain(f => f.Code == "BODY_CONTAINS_MISSING");
        result.Failures.Should().Contain(f => f.Code == "RESPONSE_TIME_EXCEEDED");
    }

    #endregion

    #region Helpers

    private static HttpTestResponse CreateResponse(
        int statusCode = 200,
        string? body = null,
        long latencyMs = 100,
        Dictionary<string, string>? headers = null)
    {
        return new HttpTestResponse
        {
            StatusCode = statusCode,
            Body = body,
            LatencyMs = latencyMs,
            Headers = headers ?? new Dictionary<string, string>(),
        };
    }

    private static ExecutionTestCaseDto CreateTestCase(
        string? expectedStatus = null,
        string? responseSchema = null,
        string? headerChecks = null,
        string? bodyContains = null,
        string? bodyNotContains = null,
        string? jsonPathChecks = null,
        int? maxResponseTime = null)
    {
        return new ExecutionTestCaseDto
        {
            TestCaseId = Guid.NewGuid(),
            Name = "Test Case",
            Expectation = new ExecutionTestCaseExpectationDto
            {
                ExpectedStatus = expectedStatus,
                ResponseSchema = responseSchema,
                HeaderChecks = headerChecks,
                BodyContains = bodyContains,
                BodyNotContains = bodyNotContains,
                JsonPathChecks = jsonPathChecks,
                MaxResponseTime = maxResponseTime,
            },
        };
    }

    #endregion
}
