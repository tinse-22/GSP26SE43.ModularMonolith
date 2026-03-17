using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.TestReporting.Commands;
using ClassifiedAds.Modules.TestReporting.Models;
using ClassifiedAds.Modules.TestReporting.Models.Requests;
using ClassifiedAds.Modules.TestReporting.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mime;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestReporting.Controllers;

[Authorize]
[ApiController]
[Route("api/test-suites/{suiteId:guid}/test-runs/{runId:guid}/reports")]
public class TestReportsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;

    public TestReportsController(Dispatcher dispatcher, ICurrentUser currentUser)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
    }

    [Authorize("Permission:GetTestRuns")]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<TestReportModel>> GenerateTestRunReport(
        Guid suiteId,
        Guid runId,
        [FromBody] GenerateTestReportRequest request)
    {
        var command = new GenerateTestReportCommand
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = _currentUser.UserId,
            ReportType = request?.ReportType,
            Format = request?.Format,
            RecentHistoryLimit = request?.RecentHistoryLimit,
        };

        await _dispatcher.DispatchAsync(command);

        return CreatedAtAction(
            nameof(GetTestRunReport),
            new { suiteId, runId, reportId = command.Result.Id },
            command.Result);
    }

    [Authorize("Permission:GetTestRuns")]
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TestReportModel>>> GetTestRunReports(Guid suiteId, Guid runId)
    {
        var result = await _dispatcher.DispatchAsync(new GetTestRunReportsQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize("Permission:GetTestRuns")]
    [HttpGet("{reportId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TestReportModel>> GetTestRunReport(Guid suiteId, Guid runId, Guid reportId)
    {
        var result = await _dispatcher.DispatchAsync(new GetTestRunReportQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            ReportId = reportId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }

    [Authorize("Permission:GetTestRuns")]
    [HttpGet("{reportId:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadTestRunReport(Guid suiteId, Guid runId, Guid reportId)
    {
        var result = await _dispatcher.DispatchAsync(new DownloadTestRunReportQuery
        {
            TestSuiteId = suiteId,
            RunId = runId,
            ReportId = reportId,
            CurrentUserId = _currentUser.UserId,
        });

        return File(
            result.Content,
            string.IsNullOrWhiteSpace(result.ContentType) ? MediaTypeNames.Application.Octet : result.ContentType,
            WebUtility.HtmlEncode(result.FileName));
    }
}
