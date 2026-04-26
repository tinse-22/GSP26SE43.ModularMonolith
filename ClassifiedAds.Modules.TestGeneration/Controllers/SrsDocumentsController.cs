using ClassifiedAds.Application;
using ClassifiedAds.Contracts.Identity.Services;
using ClassifiedAds.Modules.TestGeneration.Authorization;
using ClassifiedAds.Modules.TestGeneration.Commands;
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
/// FE-18: SRS Import + LLM Requirement Analysis + Requirement-Driven Test Generation.
/// </summary>
[Authorize]
[Produces("application/json")]
[Route("api/projects/{projectId:guid}/srs-documents")]
[ApiController]
public class SrsDocumentsController : ControllerBase
{
    private readonly Dispatcher _dispatcher;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<SrsDocumentsController> _logger;

    public SrsDocumentsController(
        Dispatcher dispatcher,
        ICurrentUser currentUser,
        ILogger<SrsDocumentsController> logger)
    {
        _dispatcher = dispatcher;
        _currentUser = currentUser;
        _logger = logger;
    }

    /// <summary>FE-18A: List all SRS documents for a project.</summary>
    [Authorize(Permissions.GetSrsDocuments)]
    [HttpGet]
    [ProducesResponseType(typeof(List<SrsDocumentModel>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<SrsDocumentModel>>> GetAll(Guid projectId)
    {
        var result = await _dispatcher.DispatchAsync(new GetSrsDocumentsQuery
        {
            ProjectId = projectId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }

    /// <summary>FE-18A: Get SRS document detail + requirements.</summary>
    [Authorize(Permissions.GetSrsDocuments)]
    [HttpGet("{srsDocumentId:guid}")]
    [ProducesResponseType(typeof(SrsDocumentModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SrsDocumentModel>> GetById(Guid projectId, Guid srsDocumentId)
    {
        var result = await _dispatcher.DispatchAsync(new GetSrsDocumentDetailQuery
        {
            ProjectId = projectId,
            SrsDocumentId = srsDocumentId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }

    /// <summary>FE-18A: Create a new SRS document (text or file upload).</summary>
    [Authorize(Permissions.AddSrsDocument)]
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SrsDocumentModel), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SrsDocumentModel>> Create(
        Guid projectId,
        [FromBody] CreateSrsDocumentRequest request)
    {
        var command = new CreateSrsDocumentCommand
        {
            ProjectId = projectId,
            CurrentUserId = _currentUser.UserId,
            Title = request.Title,
            TestSuiteId = request.TestSuiteId,
            SourceType = request.SourceType,
            RawContent = request.RawContent,
            StorageFileId = request.StorageFileId,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Created SrsDocument. Id={SrsDocumentId}, ProjectId={ProjectId}, ActorUserId={ActorUserId}",
            command.Result.Id, projectId, _currentUser.UserId);

        return Created(
            $"/api/projects/{projectId}/srs-documents/{command.Result.Id}",
            command.Result);
    }

    /// <summary>FE-18B: Trigger LLM analysis — creates SrsAnalysisJob and calls n8n.</summary>
    [Authorize(Permissions.TriggerSrsAnalysis)]
    [HttpPost("{srsDocumentId:guid}/analyze")]
    [ProducesResponseType(typeof(SrsAnalysisJobModel), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SrsAnalysisJobModel>> Analyze(Guid projectId, Guid srsDocumentId)
    {
        var command = new TriggerSrsAnalysisCommand
        {
            ProjectId = projectId,
            SrsDocumentId = srsDocumentId,
            CurrentUserId = _currentUser.UserId,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Triggered SRS analysis. SrsDocumentId={SrsDocumentId}, JobId={JobId}, ActorUserId={ActorUserId}",
            srsDocumentId, command.JobId, _currentUser.UserId);

        return Accepted(new { JobId = command.JobId, Message = "Analysis job queued. Poll /analysis-jobs/{jobId} for status." });
    }

    /// <summary>FE-18B: Poll analysis job status.</summary>
    [Authorize(Permissions.GetSrsDocuments)]
    [HttpGet("{srsDocumentId:guid}/analysis-jobs/{jobId:guid}")]
    [ProducesResponseType(typeof(SrsAnalysisJobModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SrsAnalysisJobModel>> GetJobStatus(
        Guid projectId,
        Guid srsDocumentId,
        Guid jobId)
    {
        var result = await _dispatcher.DispatchAsync(new GetSrsAnalysisJobQuery
        {
            ProjectId = projectId,
            SrsDocumentId = srsDocumentId,
            JobId = jobId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }

    /// <summary>FE-18C: List extracted SRS requirements with optional filters.</summary>
    [Authorize(Permissions.GetSrsDocuments)]
    [HttpGet("{srsDocumentId:guid}/requirements")]
    [ProducesResponseType(typeof(List<SrsRequirementModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SrsRequirementModel>>> GetRequirements(
        Guid projectId,
        Guid srsDocumentId,
        [FromQuery] Entities.SrsRequirementType? requirementType = null,
        [FromQuery] bool? isReviewed = null,
        [FromQuery] Guid? endpointId = null)
    {
        var result = await _dispatcher.DispatchAsync(new GetSrsRequirementsQuery
        {
            ProjectId = projectId,
            SrsDocumentId = srsDocumentId,
            CurrentUserId = _currentUser.UserId,
            RequirementType = requirementType,
            IsReviewed = isReviewed,
            EndpointId = endpointId,
        });

        return Ok(result);
    }

    /// <summary>FE-18C: Update requirement — title, constraints, endpoint mapping, IsReviewed flag.</summary>
    [Authorize(Permissions.ManageSrsRequirements)]
    [HttpPatch("{srsDocumentId:guid}/requirements/{requirementId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SrsRequirementModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SrsRequirementModel>> UpdateRequirement(
        Guid projectId,
        Guid srsDocumentId,
        Guid requirementId,
        [FromBody] UpdateSrsRequirementRequest request)
    {
        var command = new UpdateSrsRequirementCommand
        {
            ProjectId = projectId,
            SrsDocumentId = srsDocumentId,
            RequirementId = requirementId,
            CurrentUserId = _currentUser.UserId,
            Title = request.Title,
            TestableConstraints = request.TestableConstraints,
            EndpointId = request.EndpointId,
            IsReviewed = request.IsReviewed,
        };

        await _dispatcher.DispatchAsync(command);

        return Ok(command.Result);
    }

    /// <summary>FE-18C: Get clarification questions for a requirement.</summary>
    [Authorize(Permissions.GetSrsDocuments)]
    [HttpGet("{srsDocumentId:guid}/requirements/{requirementId:guid}/clarifications")]
    [ProducesResponseType(typeof(List<SrsRequirementClarificationModel>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<SrsRequirementClarificationModel>>> GetClarifications(
        Guid projectId,
        Guid srsDocumentId,
        Guid requirementId)
    {
        var result = await _dispatcher.DispatchAsync(new GetSrsRequirementClarificationsQuery
        {
            ProjectId = projectId,
            SrsDocumentId = srsDocumentId,
            RequirementId = requirementId,
            CurrentUserId = _currentUser.UserId,
        });

        return Ok(result);
    }

    /// <summary>FE-18C: Answer a clarification question.</summary>
    [Authorize(Permissions.ManageSrsRequirements)]
    [HttpPatch("{srsDocumentId:guid}/requirements/{requirementId:guid}/clarifications/{clarificationId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SrsRequirementClarificationModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SrsRequirementClarificationModel>> AnswerClarification(
        Guid projectId,
        Guid srsDocumentId,
        Guid requirementId,
        Guid clarificationId,
        [FromBody] AnswerClarificationRequest request)
    {
        var command = new AnswerSrsRequirementClarificationCommand
        {
            SrsRequirementId = requirementId,
            ClarificationId = clarificationId,
            CurrentUserId = _currentUser.UserId,
            UserAnswer = request.UserAnswer,
        };

        await _dispatcher.DispatchAsync(command);

        return Ok(command.Result);
    }

    /// <summary>FE-18C: Trigger Phase 1.5 refinement after critical clarifications answered.</summary>
    [Authorize(Permissions.TriggerSrsAnalysis)]
    [HttpPost("{srsDocumentId:guid}/requirements/{requirementId:guid}/refine")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RefineRequirement(
        Guid projectId,
        Guid srsDocumentId,
        Guid requirementId)
    {
        var command = new TriggerSrsRefinementCommand
        {
            ProjectId = projectId,
            SrsDocumentId = srsDocumentId,
            RequirementId = requirementId,
            CurrentUserId = _currentUser.UserId,
        };

        await _dispatcher.DispatchAsync(command);

        _logger.LogInformation(
            "Triggered SRS refinement. RequirementId={RequirementId}, JobId={JobId}, ActorUserId={ActorUserId}",
            requirementId, command.JobId, _currentUser.UserId);

        return Accepted(new { JobId = command.JobId, Message = "Refinement job queued. Poll /analysis-jobs/{jobId} for status." });
    }

    /// <summary>FE-18A: Update SRS document (e.g. link to a test suite).</summary>
    [Authorize(Permissions.AddSrsDocument)]
    [HttpPatch("{srsDocumentId:guid}")]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(SrsDocumentModel), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SrsDocumentModel>> Update(
        Guid projectId,
        Guid srsDocumentId,
        [FromBody] UpdateSrsDocumentRequest request)
    {
        var command = new UpdateSrsDocumentCommand
        {
            ProjectId = projectId,
            SrsDocumentId = srsDocumentId,
            CurrentUserId = _currentUser.UserId,
            TestSuiteId = request.TestSuiteId,
            ClearTestSuiteId = request.ClearTestSuiteId,
        };

        await _dispatcher.DispatchAsync(command);

        return Ok(command.Result);
    }

    /// <summary>FE-18A: Soft-delete SRS document.</summary>
    [Authorize(Permissions.DeleteSrsDocument)]
    [HttpDelete("{srsDocumentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid projectId, Guid srsDocumentId)
    {
        await _dispatcher.DispatchAsync(new DeleteSrsDocumentCommand
        {
            ProjectId = projectId,
            SrsDocumentId = srsDocumentId,
            CurrentUserId = _currentUser.UserId,
        });

        return NoContent();
    }
}
