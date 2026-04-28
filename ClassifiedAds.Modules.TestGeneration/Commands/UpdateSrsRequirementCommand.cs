using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using ClassifiedAds.Modules.TestGeneration.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class UpdateSrsRequirementCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid RequirementId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string Title { get; set; }

    public string TestableConstraints { get; set; }

    public Guid? EndpointId { get; set; }

    /// <summary>
    /// When true, explicitly removes the endpoint mapping regardless of EndpointId value.
    /// Mirrors UpdateSrsDocumentCommand.ClearTestSuiteId pattern.
    /// </summary>
    public bool ClearEndpointId { get; set; }

    public bool? IsReviewed { get; set; }

    public SrsRequirementModel Result { get; set; }
}

public class UpdateSrsRequirementCommandHandler : ICommandHandler<UpdateSrsRequirementCommand>
{
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;

    public UpdateSrsRequirementCommandHandler(
        IRepository<SrsRequirement, Guid> requirementRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository)
    {
        _requirementRepository = requirementRepository;
        _srsDocumentRepository = srsDocumentRepository;
    }

    public async Task HandleAsync(UpdateSrsRequirementCommand command, CancellationToken cancellationToken = default)
    {
        // Validate SrsDocument belongs to the ProjectId and is not deleted.
        var docExists = await _srsDocumentRepository.GetQueryableSet()
            .AnyAsync(x => x.Id == command.SrsDocumentId && x.ProjectId == command.ProjectId && !x.IsDeleted, cancellationToken);

        if (!docExists)
        {
            throw new NotFoundException($"SrsDocument {command.SrsDocumentId} khong tim thay trong project {command.ProjectId}.");
        }

        var req = await _requirementRepository.FirstOrDefaultAsync(
            _requirementRepository.GetQueryableSet()
                .Where(x => x.Id == command.RequirementId && x.SrsDocumentId == command.SrsDocumentId));

        if (req == null)
        {
            throw new NotFoundException($"SrsRequirement {command.RequirementId} khong tim thay.");
        }

        if (!string.IsNullOrWhiteSpace(command.Title))
        {
            req.Title = command.Title;
        }

        if (command.TestableConstraints != null)
        {
            req.TestableConstraints = command.TestableConstraints;
        }

        // Explicit clear semantics: ClearEndpointId removes the mapping;
        // otherwise set only if a new value is provided.
        if (command.ClearEndpointId)
        {
            req.EndpointId = null;
        }
        else if (command.EndpointId.HasValue)
        {
            req.EndpointId = command.EndpointId.Value;
        }

        if (command.IsReviewed.HasValue)
        {
            req.IsReviewed = command.IsReviewed.Value;
            if (command.IsReviewed.Value)
            {
                req.ReviewedById = command.CurrentUserId;
                req.ReviewedAt = DateTimeOffset.UtcNow;
            }
        }

        await _requirementRepository.UpdateAsync(req, cancellationToken);
        await _requirementRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = SrsRequirementModel.FromEntity(req);
    }
}
