using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.TestGeneration.Authorization;
using ClassifiedAds.Modules.TestGeneration.Models;
using ClassifiedAds.Modules.TestGeneration.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Controllers;

/// <summary>
/// FE-18E: Traceability report — requirement to test case coverage matrix.
/// </summary>
[Authorize]
[Produces("application/json")]
[Route("api/projects/{projectId:guid}/test-suites/{suiteId:guid}")]
[ApiController]
public class SrsTraceabilityController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<SrsTraceabilityController> _logger;

    public SrsTraceabilityController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<SrsTraceabilityController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>FE-18E: Get requirement-to-test-case coverage matrix for a test suite.</summary>
    [Authorize(Permissions.GetSrsTraceability)]
    [HttpGet("traceability")]
    [ProducesResponseType(typeof(TraceabilityMatrix), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TraceabilityMatrix>> GetTraceability(
        Guid projectId,
        Guid suiteId,
        [FromQuery] Guid? testRunId = null)
    {
        var result = await _dispatcher.DispatchAsync(new GetSrsTraceabilityQuery
        {
            ProjectId = projectId,
            TestSuiteId = suiteId,
            CurrentUserId = _currentUser.UserId,
            TestRunId = testRunId,
        });

        return Ok(result);
    }
}
