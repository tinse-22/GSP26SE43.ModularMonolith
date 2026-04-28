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

public class AddSrsRequirementCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid CurrentUserId { get; set; }

    public string Title { get; set; }

    public string Description { get; set; }

    public SrsRequirementType RequirementType { get; set; }

    public string TestableConstraints { get; set; }

    public Guid? EndpointId { get; set; }

    public SrsRequirementModel Result { get; set; }
}

public class AddSrsRequirementCommandHandler : ICommandHandler<AddSrsRequirementCommand>
{
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;

    public AddSrsRequirementCommandHandler(
        IRepository<SrsRequirement, Guid> requirementRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository)
    {
        _requirementRepository = requirementRepository;
        _srsDocumentRepository = srsDocumentRepository;
    }

    public async Task HandleAsync(AddSrsRequirementCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            throw new ValidationException("Title la bat buoc.");
        }

        var doc = await _srsDocumentRepository.FirstOrDefaultAsync(
            _srsDocumentRepository.GetQueryableSet()
                .Where(x => x.Id == command.SrsDocumentId && x.ProjectId == command.ProjectId && !x.IsDeleted));

        if (doc == null)
        {
            throw new NotFoundException($"SrsDocument {command.SrsDocumentId} khong tim thay.");
        }

        // Auto-assign RequirementCode: REQ-{N} based on current count in document
        var existingCount = await _requirementRepository.GetQueryableSet()
            .CountAsync(x => x.SrsDocumentId == command.SrsDocumentId, cancellationToken);

        var nextNumber = existingCount + 1;
        var requirementCode = $"REQ-{nextNumber:D3}";

        // Ensure uniqueness within the document (in case of concurrent inserts)
        while (await _requirementRepository.GetQueryableSet()
                   .AnyAsync(x => x.SrsDocumentId == command.SrsDocumentId && x.RequirementCode == requirementCode, cancellationToken))
        {
            nextNumber++;
            requirementCode = $"REQ-{nextNumber:D3}";
        }

        var req = new SrsRequirement
        {
            SrsDocumentId = command.SrsDocumentId,
            RequirementCode = requirementCode,
            Title = command.Title,
            Description = command.Description,
            RequirementType = command.RequirementType,
            TestableConstraints = command.TestableConstraints,
            EndpointId = command.EndpointId,
            ConfidenceScore = 1.0f, // manually added requirements are considered fully confident
            DisplayOrder = nextNumber,
            IsReviewed = true, // manually added = already reviewed by definition
            ReviewedById = command.CurrentUserId,
            ReviewedAt = DateTimeOffset.UtcNow,
            RowVersion = Guid.NewGuid().ToByteArray(),
        };

        await _requirementRepository.AddAsync(req, cancellationToken);
        await _requirementRepository.UnitOfWork.SaveChangesAsync(cancellationToken);

        command.Result = SrsRequirementModel.FromEntity(req);
    }
}
