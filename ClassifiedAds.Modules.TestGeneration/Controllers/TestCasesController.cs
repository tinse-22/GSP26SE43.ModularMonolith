using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.TestGeneration.Authorization;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.ConfigurationOptions;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Controllers;

/// <summary>
/// Manages test cases for a test suite.
/// FE-05B: Happy-path test case generation from approved API order via n8n/LLM.
/// FE-06: Boundary/negative test case generation via rule-based mutations + LLM suggestions.
/// </summary>
[Authorize]
[Produces("application/json")]
[Route("api/test-suites/{suiteId:guid}/test-cases")]
[ApiController]
public class TestCasesController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<TestCasesController> _logger;
    private readonly N8nIntegrationOptions _n8nOptions;

    public TestCasesController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<TestCasesController> logger,
        IOptions<N8nIntegrationOptions> n8nOptions)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
        _n8nOptions = n8nOptions?.Value ?? new N8nIntegrationOptions();
    }

    /// <summary>
    /// Generate happy-path test cases from the approved API order using LLM via n8n.
    /// Requires an approved API order to exist (FE-05A gate).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.GenerateTestCases)]
    [HttpPost("generate-happy-path")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(GenerateHappyPathResultModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenerateHappyPathResultModel>> GenerateHappyPath(
        Guid suiteId,
        [FromBody] GenerateHappyPathTestCasesRequest request)
    {
        if (_n8nOptions.UseDotnetIntegrationWorkflowForGeneration)
        {
            var queueCommand = new GenerateTestCasesCommand
            {
                TestSuiteId = suiteId,
                CurrentUserId = _currentUser.UserId,
            };

            await _dispatcher.DispatchAsync(queueCommand);

            _logger.LogInformation(
                "Queued unified n8n generation workflow trigger from happy-path endpoint. TestSuiteId={TestSuiteId}, JobId={JobId}, ActorUserId={ActorUserId}",
                suiteId, queueCommand.JobId,
                _currentUser.UserId);

            return Accepted(new GenerateTestsAcceptedResponse
            {
                JobId = queueCommand.JobId,
                TestSuiteId = suiteId,
                Mode = "callback",
                Message = "Đã tạo job và đưa yêu cầu trigger n8n vào hàng đợi. Test cases sẽ được lưu qua callback endpoint.",
            });
        }

        var command = new GenerateHappyPathTestCasesCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            SpecificationId = request.SpecificationId,
            ForceRegenerate = request.ForceRegenerate,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Generated happy-path test cases. TestSuiteId={TestSuiteId}, TotalGenerated={Total}, ActorUserId={ActorUserId}",
            suiteId, command.Result?.TotalGenerated, _currentUser.UserId);

        return Created(
            $"/api/test-suites/{suiteId}/test-cases",
            command.Result);
    }

    /// <summary>
    /// Generate boundary/negative test cases using rule-based mutations and LLM suggestions.
    /// Requires an approved API order to exist (FE-05A gate).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.GenerateBoundaryNegativeTestCases)]
    [HttpPost("generate-boundary-negative")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(GenerateBoundaryNegativeResultModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenerateBoundaryNegativeResultModel>> GenerateBoundaryNegative(
        Guid suiteId,
        [FromBody] GenerateBoundaryNegativeTestCasesRequest request)
    {
        if (_n8nOptions.UseDotnetIntegrationWorkflowForGeneration)
        {
            var queueCommand = new GenerateTestCasesCommand
            {
                TestSuiteId = suiteId,
                CurrentUserId = _currentUser.UserId,
            };

            await _dispatcher.DispatchAsync(queueCommand);

            _logger.LogInformation(
                "Queued unified n8n generation workflow trigger from boundary/negative endpoint. TestSuiteId={TestSuiteId}, JobId={JobId}, ActorUserId={ActorUserId}",
                suiteId, queueCommand.JobId,
                _currentUser.UserId);

            return Accepted(new GenerateTestsAcceptedResponse
            {
                JobId = queueCommand.JobId,
                TestSuiteId = suiteId,
                Mode = "callback",
                Message = "Đã tạo job và đưa yêu cầu trigger n8n vào hàng đợi. Test cases sẽ được lưu qua callback endpoint.",
            });
        }

        var command = new GenerateBoundaryNegativeTestCasesCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            SpecificationId = request.SpecificationId,
            ForceRegenerate = request.ForceRegenerate,
            IncludePathMutations = request.IncludePathMutations,
            IncludeBodyMutations = request.IncludeBodyMutations,
            IncludeLlmSuggestions = request.IncludeLlmSuggestions,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Generated boundary/negative test cases. TestSuiteId={TestSuiteId}, TotalGenerated={Total}, ActorUserId={ActorUserId}",
            suiteId, command.Result?.TotalGenerated, _currentUser.UserId);

        return Created(
            $"/api/test-suites/{suiteId}/test-cases",
            command.Result);
    }

    /// <summary>
    /// List all test cases for a test suite.
    /// Optionally filter by test type and include disabled test cases.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.GetTestCases)]
    [HttpGet]
    [ProducesResponseType(typeof(List<TestCaseModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<TestCaseModel>>> GetAll(
        Guid suiteId,
        [FromQuery] string testType = null,
        [FromQuery] bool includeDisabled = false)
    {
        TestType? filterType = null;
        if (!string.IsNullOrWhiteSpace(testType) && Enum.TryParse<TestType>(testType, true, out var parsed))
        {
            filterType = parsed;
        }

        var result = await _dispatcher.DispatchAsync(new GetTestCasesByTestSuiteQuery
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            FilterByTestType = filterType,
            IncludeDisabled = includeDisabled,
        });

        return Ok(result);
    }

    /// <summary>
    /// Get a specific test case with full details (request, expectation, variables).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.GetTestCases)]
    [HttpGet("{testCaseId:guid}")]
    [ProducesResponseType(typeof(TestCaseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestCaseModel>> GetById(Guid suiteId, Guid testCaseId)
    {
        var result = await _dispatcher.DispatchAsync(new GetTestCaseDetailQuery
        {
            TestSuiteId = suiteId,
            TestCaseId = testCaseId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }

    /// <summary>
    /// Manually create a new test case with request, expectation, and variables.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.AddTestCase)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(TestCaseModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestCaseModel>> Add(
        Guid suiteId,
        [FromBody] AddTestCaseRequest request)
    {
        var command = new AddTestCaseCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            EndpointId = request.EndpointId,
            Name = request.Name,
            Description = request.Description,
            TestType = request.TestType,
            Priority = request.Priority,
            IsEnabled = request.IsEnabled,
            Tags = request.Tags,
            RequestHttpMethod = request.Request?.HttpMethod,
            RequestUrl = request.Request?.Url,
            RequestHeaders = request.Request?.Headers,
            RequestPathParams = request.Request?.PathParams,
            RequestQueryParams = request.Request?.QueryParams,
            RequestBodyType = request.Request?.BodyType ?? Entities.BodyType.None,
            RequestBody = request.Request?.Body,
            RequestTimeout = request.Request?.Timeout ?? 30000,
            ExpectedStatus = request.Expectation?.ExpectedStatus,
            ResponseSchema = request.Expectation?.ResponseSchema,
            HeaderChecks = request.Expectation?.HeaderChecks,
            BodyContains = request.Expectation?.BodyContains,
            BodyNotContains = request.Expectation?.BodyNotContains,
            JsonPathChecks = request.Expectation?.JsonPathChecks,
            MaxResponseTime = request.Expectation?.MaxResponseTime,
            Variables = request.Variables?.Select(v => new VariableInput
            {
                VariableName = v.VariableName,
                ExtractFrom = v.ExtractFrom,
                JsonPath = v.JsonPath,
                HeaderName = v.HeaderName,
                Regex = v.Regex,
                DefaultValue = v.DefaultValue,
            }).ToList() ?? new List<VariableInput>(),
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Created test case manually. TestSuiteId={TestSuiteId}, TestCaseId={TestCaseId}, ActorUserId={ActorUserId}",
            suiteId, command.Result?.Id, _currentUser.UserId);

        return Created(
            $"/api/test-suites/{suiteId}/test-cases/{command.Result?.Id}",
            command.Result);
    }

    /// <summary>
    /// Update an existing test case with request, expectation, and variables.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.UpdateTestCase)]
    [HttpPut("{testCaseId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(TestCaseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestCaseModel>> Update(
        Guid suiteId,
        Guid testCaseId,
        [FromBody] UpdateTestCaseRequest request)
    {
        var command = new UpdateTestCaseCommand
        {
            TestSuiteId = suiteId,
            TestCaseId = testCaseId,
            CurrentUserId = _currentUser.UserId,
            EndpointId = request.EndpointId,
            Name = request.Name,
            Description = request.Description,
            TestType = request.TestType,
            Priority = request.Priority,
            IsEnabled = request.IsEnabled,
            Tags = request.Tags,
            RequestHttpMethod = request.Request?.HttpMethod,
            RequestUrl = request.Request?.Url,
            RequestHeaders = request.Request?.Headers,
            RequestPathParams = request.Request?.PathParams,
            RequestQueryParams = request.Request?.QueryParams,
            RequestBodyType = request.Request?.BodyType ?? Entities.BodyType.None,
            RequestBody = request.Request?.Body,
            RequestTimeout = request.Request?.Timeout ?? 30000,
            ExpectedStatus = request.Expectation?.ExpectedStatus,
            ResponseSchema = request.Expectation?.ResponseSchema,
            HeaderChecks = request.Expectation?.HeaderChecks,
            BodyContains = request.Expectation?.BodyContains,
            BodyNotContains = request.Expectation?.BodyNotContains,
            JsonPathChecks = request.Expectation?.JsonPathChecks,
            MaxResponseTime = request.Expectation?.MaxResponseTime,
            Variables = request.Variables?.Select(v => new VariableInput
            {
                VariableName = v.VariableName,
                ExtractFrom = v.ExtractFrom,
                JsonPath = v.JsonPath,
                HeaderName = v.HeaderName,
                Regex = v.Regex,
                DefaultValue = v.DefaultValue,
            }).ToList() ?? new List<VariableInput>(),
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Updated test case. TestSuiteId={TestSuiteId}, TestCaseId={TestCaseId}, ActorUserId={ActorUserId}",
            suiteId, testCaseId, _currentUser.UserId);

        return Ok(command.Result);
    }

    /// <summary>
    /// Delete a test case and recalculate order for remaining cases.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.DeleteTestCase)]
    [HttpDelete("{testCaseId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid suiteId, Guid testCaseId)
    {
        var command = new DeleteTestCaseCommand
        {
            TestSuiteId = suiteId,
            TestCaseId = testCaseId,
            CurrentUserId = _currentUser.UserId,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Deleted test case. TestSuiteId={TestSuiteId}, TestCaseId={TestCaseId}, ActorUserId={ActorUserId}",
            suiteId, testCaseId, _currentUser.UserId);

        return NoContent();
    }

    /// <summary>
    /// Toggle a test case enabled/disabled status.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.UpdateTestCase)]
    [HttpPatch("{testCaseId:guid}/toggle")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(
        Guid suiteId,
        Guid testCaseId,
        [FromBody] ToggleTestCaseRequest request)
    {
        var command = new ToggleTestCaseCommand
        {
            TestSuiteId = suiteId,
            TestCaseId = testCaseId,
            CurrentUserId = _currentUser.UserId,
            IsEnabled = request.IsEnabled,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Toggled test case. TestSuiteId={TestSuiteId}, TestCaseId={TestCaseId}, IsEnabled={IsEnabled}, ActorUserId={ActorUserId}",
            suiteId, testCaseId, request.IsEnabled, _currentUser.UserId);

        return Ok();
    }

    /// <summary>
    /// Reorder test cases by providing an ordered list of test case IDs.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    [Authorize(Permissions.UpdateTestCase)]
    [HttpPatch("reorder")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder(
        Guid suiteId,
        [FromBody] ReorderTestCasesRequest request)
    {
        var command = new ReorderTestCasesCommand
        {
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            TestCaseIds = request.TestCaseIds,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Reordered test cases. TestSuiteId={TestSuiteId}, Count={Count}, ActorUserId={ActorUserId}",
            suiteId, request.TestCaseIds?.Count, _currentUser.UserId);

        return Ok();
    }
}
