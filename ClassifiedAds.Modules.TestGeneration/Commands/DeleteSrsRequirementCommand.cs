using ClassifiedAds.Application;
using ClassifiedAds.CrossCuttingConcerns.Exceptions;
using ClassifiedAds.Domain.Repositories;
using ClassifiedAds.Modules.TestGeneration.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ClassifiedAds.Modules.TestGeneration.Commands;

public class DeleteSrsRequirementCommand : ICommand
{
    public Guid ProjectId { get; set; }

    public Guid SrsDocumentId { get; set; }

    public Guid RequirementId { get; set; }

    public Guid CurrentUserId { get; set; }
}

public class DeleteSrsRequirementCommandHandler : ICommandHandler<DeleteSrsRequirementCommand>
{
    private readonly IRepository<SrsRequirement, Guid> _requirementRepository;
    private readonly IRepository<SrsDocument, Guid> _srsDocumentRepository;
    private readonly IRepository<TestCaseRequirementLink, Guid> _linkRepository;

    public DeleteSrsRequirementCommandHandler(
        IRepository<SrsRequirement, Guid> requirementRepository,
        IRepository<SrsDocument, Guid> srsDocumentRepository,
        IRepository<TestCaseRequirementLink, Guid> linkRepository)
    {
        _requirementRepository = requirementRepository;
        _srsDocumentRepository = srsDocumentRepository;
        _linkRepository = linkRepository;
    }

    public async Task HandleAsync(DeleteSrsRequirementCommand command, CancellationToken cancellationToken = default)
    {
        // Validate document ownership
        var docExists = await _srsDocumentRepository.GetQueryableSet()
            .AnyAsync(x => x.Id == command.SrsDocumentId && x.ProjectId == command.ProjectId && !x.IsDeleted, cancellationToken);

        if (!docExists)
        {
            throw new NotFoundException($"SrsDocument {command.SrsDocumentId} khong tim thay.");
        }

        var req = await _requirementRepository.FirstOrDefaultAsync(
            _requirementRepository.GetQueryableSet()
                .Where(x => x.Id == command.RequirementId && x.SrsDocumentId == command.SrsDocumentId));

        if (req == null)
        {
            throw new NotFoundException($"SrsRequirement {command.RequirementId} khong tim thay.");
        }

        // Delete all traceability links referencing this requirement first
        var links = await _linkRepository.ToListAsync(
            _linkRepository.GetQueryableSet()
                .Where(x => x.SrsRequirementId == command.RequirementId));

        if (links.Count > 0)
        {
            await _linkRepository.BulkDeleteAsync(links, cancellationToken);
        }

        _requirementRepository.Delete(req);
        await _requirementRepository.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
