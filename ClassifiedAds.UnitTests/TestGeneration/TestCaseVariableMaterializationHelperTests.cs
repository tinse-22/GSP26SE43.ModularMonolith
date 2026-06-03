using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Services;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.UnitTests.TestGeneration;

public class TestCaseVariableMaterializationHelperTests
{
    [Fact]
    public void AddRequestBodyProducerAliasVariables_Should_MapScopedAliases_ToRequestBodyFields()
    {
        var testCase = CreateProducer("{\"email\":\"admin_123@test.com\",\"password\":\"Secret123!\",\"phone\":\"0900000000\"}");

        TestCaseVariableMaterializationHelper.AddRequestBodyProducerAliasVariables(
            testCase,
            new[] { "adminEmail", "adminPassword", "customerPhone" });

        testCase.Variables.Should().Contain(x =>
            x.VariableName == "adminEmail" &&
            x.ExtractFrom == ExtractFrom.RequestBody &&
            x.JsonPath == "$.email");
        testCase.Variables.Should().Contain(x =>
            x.VariableName == "adminPassword" &&
            x.ExtractFrom == ExtractFrom.RequestBody &&
            x.JsonPath == "$.password");
        testCase.Variables.Should().Contain(x =>
            x.VariableName == "customerPhone" &&
            x.ExtractFrom == ExtractFrom.RequestBody &&
            x.JsonPath == "$.phone");
    }

    [Fact]
    public void AddRequestBodyProducerAliasVariables_Should_Not_MapTokenAliases_ToRequestBody()
    {
        var testCase = CreateProducer("{\"token\":\"not-the-runtime-token\",\"email\":\"user@test.com\"}");

        TestCaseVariableMaterializationHelper.AddRequestBodyProducerAliasVariables(
            testCase,
            new[] { "authToken", "userEmail" });

        testCase.Variables.Should().NotContain(x => x.VariableName == "authToken");
        testCase.Variables.Should().Contain(x =>
            x.VariableName == "userEmail" &&
            x.ExtractFrom == ExtractFrom.RequestBody &&
            x.JsonPath == "$.email");
    }

    [Fact]
    public void AddExplicitVariables_Should_KeepJsonPathVariable_WhenExtractFromMissing()
    {
        var testCase = CreateProducer("{}");

        TestCaseVariableMaterializationHelper.AddExplicitVariables(
            testCase,
            new List<N8nTestCaseVariable>
            {
                new() { VariableName = "adminAuthToken", JsonPath = "$.data.token" },
            });

        testCase.Variables.Should().ContainSingle(x =>
            x.VariableName == "adminAuthToken" &&
            x.ExtractFrom == ExtractFrom.ResponseBody &&
            x.JsonPath == "$.data.token");
    }

    private static TestCase CreateProducer(string body)
    {
        var testCaseId = Guid.NewGuid();
        return new TestCase
        {
            Id = testCaseId,
            Request = new TestCaseRequest
            {
                Id = Guid.NewGuid(),
                TestCaseId = testCaseId,
                HttpMethod = ClassifiedAds.Modules.TestGeneration.Entities.HttpMethod.POST,
                Url = "/any-resource",
                BodyType = BodyType.JSON,
                Body = body,
            },
        };
    }
}
