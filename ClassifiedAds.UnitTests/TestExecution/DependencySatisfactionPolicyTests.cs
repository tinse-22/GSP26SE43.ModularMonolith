using ClassifiedAds.Modules.TestExecution.Models;
using ClassifiedAds.Modules.TestExecution.Services;
using System;
using System.Collections.Generic;

namespace ClassifiedAds.UnitTests.TestExecution;

public class DependencySatisfactionPolicyTests
{
    [Fact]
    public void IsSatisfied_NullResult_ReturnsFalse()
    {
        var result = DependencySatisfactionPolicy.IsSatisfied(null);
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfied_PassedStatus_ReturnsTrue()
    {
        var result = DependencySatisfactionPolicy.IsSatisfied(new TestCaseExecutionResult
        {
            Status = "Passed",
            HttpStatusCode = 200,
        });
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfied_2xxWithExpectationMismatchOnly_ReturnsTrue()
    {
        var result = DependencySatisfactionPolicy.IsSatisfied(new TestCaseExecutionResult
        {
            Status = "Failed",
            HttpStatusCode = 201,
            FailureReasons = new List<ValidationFailureModel>
            {
                new() { Code = "STATUS_CODE_MISMATCH", Message = "Expected 200, got 201" },
                new() { Code = "RESPONSE_SCHEMA_MISMATCH", Message = "Extra fields present" },
            },
        });
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSatisfied_2xxWithTransportError_ReturnsFalse()
    {
        var result = DependencySatisfactionPolicy.IsSatisfied(new TestCaseExecutionResult
        {
            Status = "Failed",
            HttpStatusCode = 200,
            FailureReasons = new List<ValidationFailureModel>
            {
                new() { Code = "HTTP_REQUEST_ERROR", Message = "Connection refused" },
            },
        });
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfied_4xxWithExpectationMismatchOnly_ReturnsFalse()
    {
        // 4xx is not a 2xx — even if only STATUS_CODE_MISMATCH, it's not satisfied
        var result = DependencySatisfactionPolicy.IsSatisfied(new TestCaseExecutionResult
        {
            Status = "Failed",
            HttpStatusCode = 404,
            FailureReasons = new List<ValidationFailureModel>
            {
                new() { Code = "STATUS_CODE_MISMATCH", Message = "Expected 200, got 404" },
            },
        });
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfied_FailedWithNoHttpStatus_ReturnsFalse()
    {
        var result = DependencySatisfactionPolicy.IsSatisfied(new TestCaseExecutionResult
        {
            Status = "Failed",
            HttpStatusCode = null,
        });
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfied_SkippedStatus_ReturnsFalse()
    {
        var result = DependencySatisfactionPolicy.IsSatisfied(new TestCaseExecutionResult
        {
            Status = "Skipped",
            HttpStatusCode = null,
        });
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSatisfied_5xxWithExpectationMismatchOnly_ReturnsFalse()
    {
        // 5xx is server error — not 2xx, so not satisfied
        var result = DependencySatisfactionPolicy.IsSatisfied(new TestCaseExecutionResult
        {
            Status = "Failed",
            HttpStatusCode = 500,
            FailureReasons = new List<ValidationFailureModel>
            {
                new() { Code = "STATUS_CODE_MISMATCH", Message = "Expected 200, got 500" },
            },
        });
        result.Should().BeFalse();
    }
}
