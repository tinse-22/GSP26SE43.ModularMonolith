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
