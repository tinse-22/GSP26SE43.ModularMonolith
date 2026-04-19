using ClassifiedAds.Contracts.ApiDocumentation.DTOs;
using ClassifiedAds.Contracts.TestGeneration.DTOs;
using ClassifiedAds.Modules.TestExecution.Services;
using ClassifiedAds.Modules.TestExecution.Models;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.UnitTests.TestExecution;

public class PreExecutionValidatorContractTests
{
    private readonly PreExecutionValidator _sut = new();

    [Fact]
    public void Validate_Should_Fail_WhenRequiredQueryParamMissing()
    {
        var testCase = CreateBaseTestCase();
        testCase.Request.QueryParams = "{}";

        var metadata = new ApiEndpointMetadataDto
        {
            RequiredQueryParameterNames = new List<string> { "page" },
        };

        var result = _sut.Validate(testCase, CreateEnvironment(), new Dictionary<string, string>(), metadata);

        result.Errors.Should().Contain(x => x.Code == "MISSING_REQUIRED_QUERY_PARAM" && x.Target == "QueryParams.page");
    }

    [Fact]
    public void Validate_Should_Fail_WhenContractRequiresBodyButBodyMissing()
    {
        var testCase = CreateBaseTestCase();
        testCase.Request.HttpMethod = "POST";
        testCase.Request.BodyType = "None";
        testCase.Request.Body = null;

        var metadata = new ApiEndpointMetadataDto
        {
            HasRequiredRequestBody = true,
        };

        var result = _sut.Validate(testCase, CreateEnvironment(), new Dictionary<string, string>(), metadata);

        result.Errors.Should().Contain(x => x.Code == "MISSING_REQUIRED_BODY");
    }

    [Fact]
    public void Validate_Should_PassContractChecks_WhenRequiredQueryAndBodyProvided()
    {
        var testCase = CreateBaseTestCase();
        testCase.Request.QueryParams = "{\"page\":\"1\"}";
        testCase.Request.BodyType = "JSON";
        testCase.Request.Body = "{\"name\":\"sample\"}";

        var metadata = new ApiEndpointMetadataDto
        {
            RequiredQueryParameterNames = new List<string> { "page" },
            HasRequiredRequestBody = true,
        };

        var result = _sut.Validate(testCase, CreateEnvironment(), new Dictionary<string, string>(), metadata);

        result.Errors.Should().NotContain(x => x.Code == "MISSING_REQUIRED_QUERY_PARAM");
        result.Errors.Should().NotContain(x => x.Code == "MISSING_REQUIRED_BODY");
    }

    [Fact]
    public void Validate_Should_Fail_WhenRequiredBodyIsMeaninglessEmptyObject()
    {
        var testCase = CreateBaseTestCase();
        testCase.Request.BodyType = "JSON";
        testCase.Request.Body = "{}";

        var metadata = new ApiEndpointMetadataDto
        {
            HasRequiredRequestBody = true,
            Parameters = new List<ApiEndpointParameterDescriptorDto>
            {
                new()
                {
                    Name = "body",
                    Location = "Body",
                    IsRequired = true,
                    Schema = """
                    {
                      "type": "object",
                      "required": ["name"],
                      "properties": {
                        "name": { "type": "string" }
                      }
                    }
                    """,
                },
            },
        };

        var result = _sut.Validate(testCase, CreateEnvironment(), new Dictionary<string, string>(), metadata);

        result.Errors.Should().Contain(x => x.Code == "MEANINGLESS_REQUIRED_BODY");
    }

    [Fact]
    public void Validate_Should_WarnInsteadOfFail_WhenMeaninglessBodyForNegativeCase()
    {
        var testCase = CreateBaseTestCase();
        testCase.Name = "Negative Validation: POST /api/items";
        testCase.TestType = "Negative";
        testCase.Expectation = new ExecutionTestCaseExpectationDto
        {
            ExpectedStatus = "[400]",
        };
        testCase.Request.BodyType = "JSON";
        testCase.Request.Body = "{}";

        var metadata = new ApiEndpointMetadataDto
        {
            HasRequiredRequestBody = true,
            Parameters = new List<ApiEndpointParameterDescriptorDto>
            {
                new()
                {
                    Name = "body",
                    Location = "Body",
                    IsRequired = true,
                    Schema = """
                    {
                      "type": "object",
                      "required": ["name"],
                      "properties": {
                        "name": { "type": "string" }
                      }
                    }
                    """,
                },
            },
        };

        var result = _sut.Validate(testCase, CreateEnvironment(), new Dictionary<string, string>(), metadata);

        result.Errors.Should().NotContain(x => x.Code == "MEANINGLESS_REQUIRED_BODY");
        result.Warnings.Should().Contain(x => x.Code == "MEANINGLESS_REQUIRED_BODY_FOR_ERROR_CASE");
    }

    [Fact]
    public void Validate_Should_WarnAndNotFail_WhenNumericBodyPlaceholderMissing()
    {
        var testCase = CreateBaseTestCase();
        testCase.Request.BodyType = "JSON";
        testCase.Request.Body = "{\"price\":\"{{price}}\",\"stock\":\"{{stock}}\"}";

        var result = _sut.Validate(testCase, CreateEnvironment(), new Dictionary<string, string>(), endpointMetadata: null);

        result.Errors.Should().NotContain(x => x.Code == "UNRESOLVED_VARIABLE" && x.Target == "Body");
        result.Warnings.Should().Contain(x => x.Code == "NUMERIC_PLACEHOLDER_DEFAULT_FALLBACK");
    }

    [Fact]
    public void Validate_Should_WarnAndNotFail_WhenNonIdentifierBodyPlaceholderMissing()
    {
        var testCase = CreateBaseTestCase();
        testCase.Request.BodyType = "JSON";
        testCase.Request.Body = "{\"name\":\"{{name}}\"}";

        var result = _sut.Validate(testCase, CreateEnvironment(), new Dictionary<string, string>(), endpointMetadata: null);

        result.Errors.Should().NotContain(x => x.Code == "UNRESOLVED_VARIABLE" && x.Target == "Body");
        result.Warnings.Should().Contain(x => x.Code == "TEXT_PLACEHOLDER_DEFAULT_FALLBACK");
    }

    [Fact]
    public void Validate_Should_Fail_WhenIdentifierBodyPlaceholderMissing()
    {
        var testCase = CreateBaseTestCase();
        testCase.Request.BodyType = "JSON";
        testCase.Request.Body = "{\"categoryId\":\"{{categoryId}}\"}";

        var result = _sut.Validate(testCase, CreateEnvironment(), new Dictionary<string, string>(), endpointMetadata: null);

        result.Errors.Should().Contain(x => x.Code == "UNRESOLVED_VARIABLE" && x.Target == "Body");
    }

    private static ExecutionTestCaseDto CreateBaseTestCase()
    {
        return new ExecutionTestCaseDto
        {
            TestCaseId = Guid.NewGuid(),
            Name = "Contract validation case",
            Request = new ExecutionTestCaseRequestDto
            {
                HttpMethod = "POST",
                Url = "/api/items",
                Headers = "{}",
                PathParams = "{}",
                QueryParams = "{}",
                BodyType = "JSON",
                Body = "{}",
                Timeout = 30000,
            },
            Variables = Array.Empty<ExecutionVariableRuleDto>(),
        };
    }

    private static ResolvedExecutionEnvironment CreateEnvironment()
    {
        return new ResolvedExecutionEnvironment
        {
            BaseUrl = "https://api.example.com",
            Variables = new Dictionary<string, string>(),
            DefaultHeaders = new Dictionary<string, string>(),
            DefaultQueryParams = new Dictionary<string, string>(),
        };
    }
}
