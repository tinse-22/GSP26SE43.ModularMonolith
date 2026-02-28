using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.TestGeneration.Authorization;
using ClassifiedAds.Modules.TestGeneration.Commands;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Models.Requests;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Controllers;

/// <summary>
/// Manages test cases for a test suite.
/// FE-05B: Happy-path test case generation from approved API order via n8n/LLM.
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

    public TestCasesController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<TestCasesController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>
    /// Generate happy-path test cases from the approved API order using LLM via n8n.
    /// Requires an approved API order to exist (FE-05A gate).
    /// </summary>
    [Authorize(Permissions.GenerateTestCases)]
    [HttpPost("generate-happy-path")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(GenerateHappyPathResultModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GenerateHappyPathResultModel>> GenerateHappyPath(
        Guid suiteId,
        [FromBody] GenerateHappyPathTestCasesRequest request)
    {
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
    /// List all test cases for a test suite.
    /// Optionally filter by test type and include disabled test cases.
    /// </summary>
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
            FilterByTestType = filterType,
            IncludeDisabled = includeDisabled,
        });

        return Ok(result);
    }

    /// <summary>
    /// Get a specific test case with full details (request, expectation, variables).
    /// </summary>
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
        });

        return Ok(result);
    }
}
